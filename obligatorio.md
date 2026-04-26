# PharmaGo — Obligatorio AIOps

## Descripción del proyecto

PharmaGo es una aplicación de gestión de farmacias desplegada en Kubernetes usando Minikube. La arquitectura incluye:

- **Frontend**: Angular servido con Nginx (`pharmago-ui`)
- **Backend**: API Gateway, Users Service y Pharmacy Service (ASP.NET Core)
- **Base de datos**: SQL Server Express (`pharmago-db`)
- **Observabilidad**: Prometheus, Grafana, Elasticsearch, Kibana, Fluent Bit, OpenTelemetry Collector

---

## Despliegue del cluster

### 1. Levantar Minikube

```bash
minikube start --memory=7168 --cpus=4
```

Se usan 7168 MB de RAM para dar margen suficiente a Elasticsearch (1.5Gi de heap) y SQL Server (1.5Gi) corriendo simultáneamente en el mismo nodo.

### 2. Construir imágenes Docker

Las imágenes de backend se construyen desde la carpeta `Backend/` usando el Dockerfile de cada servicio:

```bash
cd "Implementacion K8S/Codigo/Backend"
docker build -f PharmaGo.UsersService/Dockerfile -t pharmago-users-service:latest .
docker build -f PharmaGo.PharmacyService/Dockerfile -t pharmago-pharmacy-service:latest .
docker build -f PharmaGo.ApiGateway/Dockerfile -t pharmago-api-gateway:latest .
```

La imagen del frontend se construye desde la carpeta `Frontend/`:

```bash
cd "Implementacion K8S/Codigo/Frontend"
docker build -f Dockerfile -t pharmago-ui:latest .
```

### 3. Cargar imágenes en Minikube

Minikube no comparte el daemon de Docker del host, por lo que las imágenes deben cargarse explícitamente con `minikube image load`:

```bash
minikube image load pharmago-ui:latest
minikube image load pharmago-api-gateway:latest
minikube image load pharmago-pharmacy-service:latest
minikube image load pharmago-users-service:latest
```

Verificar que las cuatro están disponibles:

```bash
minikube image ls | grep pharmago
```

### 4. Aplicar manifiestos

Desde `Implementacion K8S/Codigo/k8s/`, se usa el script automatizado que aplica los recursos en el orden correcto:

```bash
./apply-k8s.sh
```

El script realiza los siguientes pasos en orden:
1. Etiqueta el nodo con `node-type=all` (requerido por el `nodeAffinity` de todos los pods)
2. Namespace `pharmago`
3. Secrets (credenciales de BD)
4. ConfigMaps (Prometheus, OTEL Collector, Grafana, Fluent Bit)
5. StorageClass y PersistentVolumes
6. Base de datos — espera hasta que el pod esté `Ready`
7. Elasticsearch — espera hasta que esté `Ready`, luego el resto de observabilidad
8. Servicios backend (Users, Pharmacy, API Gateway)
9. Frontend

### 5. Verificar estado

```bash
kubectl get pods -n pharmago
```

Todos los pods deben estar en estado `1/1 Running`.

---

## Alta disponibilidad — Rolling Update Strategy

### Contexto

Los deployments del backend ya contaban con health probes configuradas:

- **`startupProbe`**: le da tiempo al servicio para arrancar antes de que las otras probes entren en acción
- **`readinessProbe`**: indica a Kubernetes cuándo el pod está listo para recibir tráfico
- **`livenessProbe`**: detecta pods colgados y los reinicia automáticamente

Sin embargo, los tres deployments de backend no tenían definida una estrategia de rolling update explícita, por lo que usaban el default de Kubernetes (`maxUnavailable: 25%`, `maxSurge: 25%`). Con una sola réplica, esto implica que durante un deploy puede haber un instante con 0 pods disponibles.

### Cambio aplicado

Se agregó el bloque `strategy` en los tres deployments del backend:

```yaml
spec:
  replicas: 1
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxUnavailable: 0
      maxSurge: 1
```

**Archivos modificados:**
- `k8s/deployments/backend/users-service-deployment.yaml`
- `k8s/deployments/backend/pharmacy-service-deployment.yaml`
- `k8s/deployments/backend/api-gateway-deployment.yaml`

**Significado de los parámetros:**
- `maxUnavailable: 0` — nunca se termina el pod viejo antes de que el nuevo esté `Ready`
- `maxSurge: 1` — se permite levantar 1 pod extra durante el deploy (temporalmente 2 pods)

Los cambios se aplicaron con:

```bash
kubectl apply -f deployments/backend/users-service-deployment.yaml
kubectl apply -f deployments/backend/pharmacy-service-deployment.yaml
kubectl apply -f deployments/backend/api-gateway-deployment.yaml
```

### Prueba de funcionamiento

Se verificó el comportamiento en los tres deployments forzando un rollout restart y observando el ciclo de vida de los pods en tiempo real.

#### pharmago-users-service

```bash
kubectl rollout restart deployment pharmago-users-service -n pharmago
kubectl get pods -n pharmago -l app=pharmago-users-service -w
```

```
NAME                                      READY   STATUS        RESTARTS      AGE
pharmago-users-service-7558c788b6-b9hz5   0/1     Running       0             7s
pharmago-users-service-75f55796b-w7cvw    1/1     Running       2 (57m ago)   3d1h
pharmago-users-service-7558c788b6-b9hz5   1/1     Running       0             21s
pharmago-users-service-75f55796b-w7cvw    1/1     Terminating   2 (57m ago)   3d1h
pharmago-users-service-75f55796b-w7cvw    0/1     Completed     2             3d1h
```

#### pharmago-pharmacy-service

```bash
kubectl rollout restart deployment pharmago-pharmacy-service -n pharmago
kubectl get pods -n pharmago -l app=pharmago-pharmacy-service -w
```

```
NAME                                         READY   STATUS        RESTARTS     AGE
pharmago-pharmacy-service-5789d5b8c-4tdbd    1/1     Running       2 (3d ago)   3d1h
pharmago-pharmacy-service-67c75dbc46-p7zbl   0/1     Running       0            6s
pharmago-pharmacy-service-67c75dbc46-p7zbl   1/1     Running       0            21s
pharmago-pharmacy-service-5789d5b8c-4tdbd    1/1     Terminating   2 (3d ago)   3d1h
pharmago-pharmacy-service-5789d5b8c-4tdbd    0/1     Completed     2 (3d ago)   3d1h
```

#### pharmago-api-gateway

```bash
kubectl rollout restart deployment pharmago-api-gateway -n pharmago
kubectl get pods -n pharmago -l app=pharmago-api-gateway -w
```

```
NAME                                    READY   STATUS        RESTARTS     AGE
pharmago-api-gateway-5555768b5b-hh7k9   0/1     Running       0            8s
pharmago-api-gateway-b9b878fff-lv7kg    1/1     Running       1 (3d ago)   3d1h
pharmago-api-gateway-5555768b5b-hh7k9   1/1     Running       0            21s
pharmago-api-gateway-b9b878fff-lv7kg    1/1     Terminating   1 (3d ago)   3d1h
pharmago-api-gateway-b9b878fff-lv7kg    0/1     Completed     1            3d1h
```

### Análisis del resultado

El comportamiento fue idéntico en los tres servicios:

1. El pod nuevo arrancó mientras el pod viejo seguía en `1/1 Running`
2. Una vez que el pod nuevo pasó la `readinessProbe` y llegó a `1/1 Running`, recién entonces el pod viejo pasó a `Terminating`
3. En ningún momento hubo 0 pods disponibles para recibir tráfico

Esto confirma que la estrategia `maxUnavailable: 0` está funcionando correctamente en los tres deployments del backend: el servicio nunca se interrumpe durante un deploy.

---

## Circuit Breaker

### Contexto

El Circuit Breaker es un patrón de alta disponibilidad que mitiga el alto volumen de solicitudes fallidas. Cuando un servicio downstream falla repetidamente, el circuito "se abre" y las llamadas posteriores fallan de inmediato en lugar de acumular timeouts, protegiendo al sistema de cascadas de fallas.

### Implementación en dos capas

#### Capa 1 — API Gateway (YARP Active Health Check)

YARP ya tenía configurado un mecanismo de health check en `appsettings.json` para ambos clusters, pero estaba deshabilitado. Se habilitó cambiando `Enabled: false` a `Enabled: true` en los dos clusters:

```json
"HealthCheck": {
  "Active": {
    "Enabled": true,
    "Interval": "00:00:10",
    "Timeout": "00:00:05",
    "Policy": "ConsecutiveFailures",
    "Path": "/health"
  }
}
```

**Efecto:** YARP hace un GET a `/health` de cada backend cada 10 segundos. Si detecta fallas consecutivas, marca el destino como `Unhealthy` y deja de routear tráfico hacia él hasta que se recupere.

#### Capa 2 — Inter-service (Polly Circuit Breaker)

Se agregó `Microsoft.Extensions.Http.Polly` v6.0.8 como dependencia en los Factory projects y se configuró la política de circuit breaker en el registro del `HttpClient` de cada servicio:

**`PharmaGo.UsersService.Factory/ServiceFactory.cs`** (llamadas al Pharmacy Service):

```csharp
serviceCollection.AddHttpClient<PharmacyServiceClient>(client =>
{
    client.BaseAddress = new Uri(serviceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30)));
```

**`PharmaGo.PharmacyService.Factory/ServiceFactory.cs`** (llamadas al Users Service): configuración idéntica sobre `UsersServiceClient`.

**Parámetros:**
- `5` fallas consecutivas → abre el circuito
- `30 segundos` abierto → luego pasa a half-open y prueba una request

### Prueba de funcionamiento — YARP Health Check

Se simuló la caída del users-service y se verificó el comportamiento del gateway:

```bash
# 1. Bajar el users-service
kubectl scale deployment pharmago-users-service -n pharmago --replicas=0

# 2. Esperar ~15s que YARP detecte la falla, luego hacer un request
Invoke-WebRequest -Uri "http://127.0.0.1:5000/api/users" -UseBasicParsing
# → 503 Service Unavailable (YARP cortó el tráfico al destino unhealthy)

# 3. Restaurar el servicio
kubectl scale deployment pharmago-users-service -n pharmago --replicas=1

# 4. Esperar ~30s y repetir el request
Invoke-WebRequest -Uri "http://127.0.0.1:5000/api/users" -UseBasicParsing
# → 405 Method Not Allowed (YARP recuperó y routea al servicio vivo)
```

**Análisis del resultado:**

1. Con `users-service` caído, YARP detectó fallas consecutivas en el health check y devolvió **503** — el tráfico fue cortado sin esperar timeouts
2. Una vez restaurado el servicio, YARP lo detectó como `Healthy` y retomó el routing automáticamente — el **405** confirma que la request llegó al servicio (error de aplicación, no de gateway)

La autocuración es completa: el sistema detecta la falla, corta el tráfico, y se recupera sin intervención manual.

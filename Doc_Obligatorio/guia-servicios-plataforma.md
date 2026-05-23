# Guia de servicios y material de referencia

Este documento resume que levanta la plataforma PharmaGo en Kubernetes, para que sirve cada componente, como acceder a las herramientas y que archivos Markdown conviene leer para entender el obligatorio.

## Objetivo del trabajo

El obligatorio pide mejorar la respuesta a incidentes de un sistema existente. La rubrica se organiza en estos ejes:

| Eje | Que pide |
| --- | --- |
| Implementacion de plataforma | Ejecutar el sistema sobre Kubernetes, con cada microservicio en un deployment independiente, y documentar un diagrama de despliegue. |
| Alta disponibilidad | Lograr que los servicios se recuperen ante fallas de runtime y mitigar alto volumen de solicitudes fallidas. |
| Despliegues seguros | Reemplazar componentes garantizando disponibilidad durante el cambio y documentar la tecnica elegida. |
| Telemetria | Logs JSON, trazas OTLP, metricas de app/infra, Prometheus, Grafana, Elastic/Kibana y al menos 5 alertas. |
| Deteccion de anomalias | Implementar notebooks con IsolationForest y Support Vector Machine sobre datasets docentes. |
| Plan de contencion | Definir un plan de respuesta a incidentes siguiendo el framework de Atlassian. |
| Scripts de caos | Simular requests, CPU, memoria, storage, red y desconexion de componentes internos. |
| Defensa | Ejecutar caos, mostrar efectos en telemetria y explicar por que el sistema responde como responde. |

## Como levantar desde cero

Desde `Implementacion K8S/Codigo/k8s`:

```bash
minikube start --memory=8192 --cpus=4
./build-images.sh
./apply-k8s.sh
kubectl get pods -n pharmago
./port-forward.sh
```

Si los scripts no tienen permisos de ejecucion:

```bash
chmod +x build-images.sh apply-k8s.sh port-forward.sh cleanup.sh
```

Para apagar los port-forward:

```bash
./port-forward.sh --stop
```

## Servicios de aplicacion

| Servicio Kubernetes | Tipo | Puerto interno | Acceso local con port-forward | Para que sirve |
| --- | --- | ---: | --- | --- |
| `pharmago-ui` | Frontend Angular | 80 | `http://127.0.0.1:4200` | Interfaz web de PharmaGo. Consume el API Gateway. |
| `pharmago-api-gateway` | API Gateway .NET/YARP | 80 | `http://127.0.0.1:5000` | Punto de entrada HTTP. Enruta requests hacia Users y Pharmacy. Implementa rate limiting y propagacion de correlation id. |
| `pharmago-users-service` | Microservicio .NET | 80 | `http://127.0.0.1:5001` | Gestion de usuarios, roles, invitaciones y login. Ejecuta migraciones de base al iniciar. |
| `pharmago-pharmacy-service` | Microservicio .NET | 80 | `http://127.0.0.1:5002` | Gestion de farmacias, medicamentos, compras, stock, presentaciones, unidades y exportacion. |
| `pharmago-db` | SQL compatible con SQL Server | 1433 | Opcional: `127.0.0.1,11433` si el script lo expone | Base de datos usada por los microservicios. |

Credenciales de base:

```text
Usuario: sa
Password: Str0ngP@ssword!
Servidor interno Kubernetes: pharmago-db
Puerto: 1433
Database: 
Connection string interno: Server=pharmago-db;Database=PharmaDb;User Id=sa;Password=Str0ngP@ssword!
```

Nota importante para Apple Silicon/ARM: el deployment de DB usa `mcr.microsoft.com/azure-sql-edge:latest` porque `mcr.microsoft.com/mssql/server:2019-latest` falla en Minikube ARM con `exec format error`. En Windows x64 deberia funcionar tambien con SQL Edge.

## Servicios de observabilidad

| Servicio Kubernetes | Tipo | Puerto interno | Acceso local con port-forward | Para que sirve |
| --- | --- | ---: | --- | --- |
| `prometheus` | Metricas | 9090 | `http://127.0.0.1:9090` | Recolecta metricas de app, OTEL Collector, Node Exporter y cAdvisor. |
| `grafana` | Visualizacion y alertas | 3000 | `http://127.0.0.1:3000` | Dashboards de aplicacion e infraestructura. Lugar natural para evidenciar metricas y alertas. |
| `otlp-collector` | OpenTelemetry Collector | 4317, 8889 | No suele abrirse directo | Recibe metricas OTLP por gRPC y las expone para Prometheus. |
| `node-exporter` | Metricas de nodo | 9100 | No suele abrirse directo | Expone CPU, memoria, filesystem y red del nodo Minikube. |
| `elasticsearch` | Almacenamiento de logs | 9200, 9300 | Normalmente interno | Recibe logs procesados por Fluent Bit. |
| `kibana` | Consulta de logs | 5601 | `http://127.0.0.1:5601` | Permite buscar logs de Kubernetes y logs JSON de las aplicaciones. |
| `fluent-bit` | Agente de logs | 2020 | No suele abrirse directo | Lee logs de contenedores, parsea JSON y envia a Elasticsearch. |

Credenciales:

```text
Grafana: admin / admin
Kibana: sin usuario en la configuracion actual
Prometheus: sin usuario en la configuracion actual
```

## Que revisar para validar que todo esta vivo

```bash
kubectl get pods -n pharmago
kubectl get svc -n pharmago
curl http://127.0.0.1:5000/metrics
curl http://127.0.0.1:5001/health
curl http://127.0.0.1:5002/health
```

En Grafana revisar:

```text
Dashboard: PharmaGo - Overview
Dashboard: PharmaGo - Infra
```

En Prometheus revisar targets:

```text
http://127.0.0.1:9090/targets
```

En Kibana revisar indices/logs con prefijo:

```text
pharmago-logs
```

## Componentes y relacion con la rubrica

| Rubrica | Componentes relacionados | Evidencia util |
| --- | --- | --- |
| Kubernetes | `namespace.yaml`, `deployments/`, `services/`, `configmaps/`, `secrets/`, `persistent-volumes/` | `kubectl get pods -n pharmago`, `kubectl get deploy -n pharmago`, diagrama de despliegue. |
| Alta disponibilidad | Probes en deployments, replicas, rate limiting del gateway | Reiniciar/matar pods y medir recuperacion; prueba de 429 ante carga. |
| Despliegues seguros | Deployments backend/frontend y estrategia de rollout elegida | `kubectl rollout status`, prueba de actualizacion sin indisponibilidad. |
| Telemetria | Prometheus, Grafana, OTEL Collector, Fluent Bit, Elasticsearch, Kibana, middleware de metricas/logs | Dashboards, targets Prometheus, busquedas Kibana, alertas. |
| Anomalias | Notebooks a crear | Resultados de IsolationForest y SVM. |
| Incidentes | Plan/runbook a documentar | Roles, severidades, fases Atlassian, mitigaciones y postmortem. |
| Caos | Scripts en `chaos-engineering/` | Ejecucion de scripts y correlacion con metricas/logs. |

## Scripts de caos existentes

| Script | Uso | Que simula |
| --- | --- | --- |
| `chaos-engineering/load-requests.sh` | `./load-requests.sh [cantidad] [url]` | Alto volumen de requests contra un endpoint. Sirve para observar throughput, errores y rate limiting. |
| `chaos-engineering/cpu-spike.sh` | `./cpu-spike.sh [deployment] [segundos]` | Sobrecarga de CPU dentro de un pod. |
| `chaos-engineering/ram-spike.sh` | `./ram-spike.sh [deployment] [MB]` | Sobrecarga de memoria dentro de un pod. |
| `chaos-engineering/volume-spike.sh` | `./volume-spike.sh [deployment] [MB]` | Llenado de storage/ephemeral storage dentro de un pod. |

Faltantes segun la letra:

```text
- Interrupcion de trafico de red.
- Desconexion de componentes internos de la aplicacion.
```

## Markdown disponibles y para que sirve cada uno

| Archivo | Para que sirve |
| --- | --- |
| `Doc_Obligatorio/AIOps - Obligatorio.md` | Letra oficial del obligatorio y rubrica. Es el documento base para saber que hay que entregar. |
| `Doc_Obligatorio/guia-servicios-plataforma.md` | Esta guia. Resume servicios, accesos, credenciales, comandos y relacion con la rubrica. |
| `Implementacion K8S/Codigo/QUICKSTART.md` | Arranque rapido con Docker Compose y panorama general de servicios. |
| `Implementacion K8S/Codigo/k8s/README-k8s.md` | Guia principal para desplegar en Minikube: prerequisitos, build de imagenes, apply, acceso y troubleshooting. |
| `Implementacion K8S/Codigo/tutoriales/quickstart.md` | Version corta del quickstart de Kubernetes. Util para comandos minimos. |
| `Implementacion K8S/Codigo/chaos-engineering/README.md` | Resumen de scripts de caos existentes y como ejecutarlos. |
| `Implementacion K8S/Codigo/Backend/RATE_LIMITING_GUIDE.md` | Explica el rate limiting del API Gateway, modos por IP/usuario y como probarlo. Sirve para alta disponibilidad/resiliencia ante alto volumen de fallas. |
| `Implementacion K8S/Codigo/tutoriales/rate-limit/README.md` | Guia corta para ejecutar el test de rate limit. |
| `Implementacion K8S/Codigo/tutoriales/rate-limit/como-tunear-rate-limit.md` | Explica como ajustar los limites de rate limiting. |
| `Implementacion K8S/Codigo/tutoriales/como-funciona-prometheus.md` | Explica Prometheus y como consultar metricas. |
| `Implementacion K8S/Codigo/tutoriales/como-usar-grafana.md` | Explica como entrar a Grafana y usar dashboards. |
| `Implementacion K8S/Codigo/tutoriales/como-revisar-logs-kibana.md` | Explica como revisar logs en Kibana. |
| `Implementacion K8S/Codigo/tutoriales/como-agregar-metrica-nueva.md` | Guia para agregar metricas custom a la aplicacion. |
| `Implementacion K8S/Codigo/tutoriales/logging/como-agregar-log-estructurado.md` | Guia para agregar logs estructurados en JSON. |
| `Implementacion K8S/Codigo/Backend/explicacion-volumenes.md` | Explica volumenes/persistencia. Util para DB, Prometheus, Grafana y Elasticsearch. |
| `Implementacion K8S/Codigo/Backend/MICROSERVICES_MIGRATION_SUMMARY.md` | Resume la migracion del backend a microservicios. Sirve para entender la separacion gateway/users/pharmacy. |
| `Implementacion K8S/Codigo/Frontend/README.md` | Documentacion propia del frontend Angular. |

## Orden recomendado de lectura

1. `Doc_Obligatorio/AIOps - Obligatorio.md`
2. `Implementacion K8S/Codigo/k8s/README-k8s.md`
3. `Implementacion K8S/Codigo/QUICKSTART.md`
4. `Implementacion K8S/Codigo/Backend/MICROSERVICES_MIGRATION_SUMMARY.md`
5. `Implementacion K8S/Codigo/Backend/RATE_LIMITING_GUIDE.md`
6. `Implementacion K8S/Codigo/tutoriales/como-funciona-prometheus.md`
7. `Implementacion K8S/Codigo/tutoriales/como-usar-grafana.md`
8. `Implementacion K8S/Codigo/tutoriales/como-revisar-logs-kibana.md`
9. `Implementacion K8S/Codigo/chaos-engineering/README.md`

## Notas para el informe

Conviene transformar esta guia en evidencia del informe de esta forma:

| Seccion del informe | Que tomar de esta guia |
| --- | --- |
| Implementacion de plataforma | Lista de deployments, services, namespace, configmaps, secrets y persistencia. |
| Alta disponibilidad | Probes, rate limiting, comandos de validacion y pruebas de recuperacion. |
| Telemetria | Servicios de Prometheus/Grafana/Kibana/OTEL/Fluent Bit y evidencias. |
| Scripts de caos | Tabla de scripts existentes y faltantes. |
| Defensa | URLs, credenciales, comandos de validacion y secuencia de demo. |


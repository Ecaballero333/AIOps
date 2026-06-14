# Chaos Engineering

Requiere: app en K8s + port-forward activo.

| Script | Uso |
|--------|-----|
| `cpu-spike.sh` | `./cpu-spike.sh [--scope all|pod|deploy] [--target pod/app] [--intensity low|medium|high]` |
| `ram-spike.sh` | `./ram-spike.sh [deploy] [MB]` |
| `volume-spike.sh` | `./volume-spike.sh [deploy] [MB]` |
| `load-requests.sh` | `./load-requests.sh [N] [url]` |
| `business-metrics-chaos.sh` | `./business-metrics-chaos.sh [-f funcionalidad] [-e yes|no|mixed] [-n cantidad] [-d ms]` |
| `network-interruption.sh` | `./network-interruption.sh [deploy] [seg] [namespace]` |
| `disconnect-component.sh` | `./disconnect-component.sh [deploy] [seg] [namespace]` |

Default deploy: pharmago-api-gateway

## Cobertura de caos

| Requisito | Script |
|-----------|--------|
| Inyeccion de requests | `load-requests.sh` |
| Metricas de negocio login/compras | `business-metrics-chaos.sh` |
| Sobrecarga de CPU | `cpu-spike.sh` |
| Sobrecarga de memoria | `ram-spike.sh` |
| Sobrecarga de storage | `volume-spike.sh` |
| Interrupcion de trafico de red | `network-interruption.sh` |
| Desconexion de componentes internos | `disconnect-component.sh` |

**Ver impacto en Grafana:** Dashboard "PharmaGo - Infra" → paneles "CPU por pod (pharmago)" y "Memoria por pod (pharmago)".

## Chaos de CPU

`cpu-spike.sh` abre un menu interactivo para decidir el alcance del ataque:

```text
1) Todas las instancias de todos los servicios PharmaGo
2) Una instancia/pod particular, copiando y pegando el nombre
3) Todas las instancias de un servicio/deployment
```

Tambien permite elegir intensidad:

```text
low     ~50% de 1 core
medium  ~75% de 1 core
high    ~95% de 1 core, buscando superar 90%
```

Uso interactivo:

```sh
./cpu-spike.sh
```

Uso parametrizado:

```sh
./cpu-spike.sh --scope all --intensity high
./cpu-spike.sh --scope pod --target pharmago-api-gateway-xxxxx --intensity medium
./cpu-spike.sh --scope deploy --target pharmago-users-service --intensity low
```

El script queda corriendo hasta que lo cortes con `Ctrl+C`. Al finalizar ejecuta cleanup remoto y mata el loop de CPU controlado que creo en cada pod. Para ver 75% o 90% en el dashboard actual, los deployments backend tienen que permitir `limits.cpu: "1000m"`.

## Chaos de metricas de negocio

`business-metrics-chaos.sh` dispara requests contra el API Gateway para mover el dashboard "PharmaGo - Business" y, por efecto colateral, tambien las golden signals: trafico, errores y latencia.

Uso interactivo:

```sh
./business-metrics-chaos.sh
```

Uso parametrizado:

```sh
./business-metrics-chaos.sh -f login -e yes -n 200 -d 20
./business-metrics-chaos.sh -f purchase-create -e mixed -n 100 -d 50
./business-metrics-chaos.sh -f all -e mixed -n 100 -d 100 --username Juan --password 123456
./business-metrics-chaos.sh -f purchase-status -e no -n 20 --token <TOKEN>
```

Funcionalidades disponibles:

```text
login
purchase-create
purchase-approve
purchase-reject
purchase-status
all
```

Modo de error:

```text
yes    fuerza payloads/credenciales invalidas
no     intenta requests validos
mixed  alterna requests validos e invalidos
```

Metricas cubiertas:

```text
pharmago_business_events_total
pharmago_login_attempts_total
pharmago_purchases_created_total
pharmago_purchase_amount_total
pharmago_purchase_items_total
pharmago_purchase_status_changes_total
```

Notas operativas:

- Para login exitoso se necesita `--username` y `--password`, o las variables `PHARMAGO_USERNAME` y `PHARMAGO_PASSWORD`.
- Para approve/reject exitoso se necesita `--token`, o credenciales validas para que el script obtenga token via login.
- El script descubre automaticamente una droga/farmacia con stock usando `/api/drug`; si no puede, usa `--pharmacy-id` y `--drug-code`.
- Si no pasas `--purchase-id` en approve/reject, el script crea una compra nueva y usa el id devuelto para cambiar su estado.
- Si no hay token en approve/reject, se movera error rate HTTP, pero no necesariamente la metrica de negocio `purchase_status_change` porque el request puede quedar bloqueado por autorizacion antes del controller.
- Default URL: `http://127.0.0.1:5000`, esperando port-forward del API Gateway.
- En modo interactivo, el menu pregunta al final si queres imprimir el log de cada request; equivale a usar `--verbose`.

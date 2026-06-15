# Chaos Engineering

Requiere: app en K8s + port-forward activo.

| Script | Uso |
|--------|-----|
| `cpu-spike.sh` | `./cpu-spike.sh [deploy] [seg]` |
| `ram-spike.sh` | `./ram-spike.sh [deploy] [MB]` |
| `volume-spike.sh` | `./volume-spike.sh [deploy] [MB]` |
| `load-requests.sh` | `./load-requests.sh [N] [url]` |
| `app-latency-ramp.sh` | `./app-latency-ramp.sh [deploy] [namespace] [seconds_per_step] [profile] [include_health]` |
| `network-interruption.sh` | `./network-interruption.sh [deploy] [seg] [namespace]` |
| `pharmacy-network-latency.sh` | `./pharmacy-network-latency.sh [delay_ms] [namespace] [deployment]` |
| `disconnect-component.sh` | `./disconnect-component.sh [deploy] [seg] [namespace]` |
| `disconnect-db-service.sh` | `./disconnect-db-service.sh [service] [namespace] [app_label_original]` |

Default deploy: pharmago-api-gateway

## Cobertura de caos

| Requisito | Script |
|-----------|--------|
| Inyeccion de requests | `load-requests.sh` |
| Sobrecarga de CPU | `cpu-spike.sh` |
| Sobrecarga de memoria | `ram-spike.sh` |
| Sobrecarga de storage | `volume-spike.sh` |
| Latencia progresiva medida por la app | `app-latency-ramp.sh` |
| Interrupcion de trafico de red | `network-interruption.sh` |
| Latencia de red en Pharmacy/readiness | `pharmacy-network-latency.sh` |
| Desconexion de componentes internos | `disconnect-component.sh` |
| BD corriendo pero sin endpoints en el Service | `disconnect-db-service.sh` |

**Ver impacto en Grafana:** Dashboard "PharmaGo - Infra" → paneles "CPU por pod (pharmago)" y "Memoria por pod (pharmago)".

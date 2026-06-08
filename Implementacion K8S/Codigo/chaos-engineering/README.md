# Chaos Engineering

Requiere: app en K8s + port-forward activo.

| Script | Uso |
|--------|-----|
| `cpu-spike.sh` | `./cpu-spike.sh [deploy] [seg]` |
| `ram-spike.sh` | `./ram-spike.sh [deploy] [MB]` |
| `volume-spike.sh` | `./volume-spike.sh [deploy] [MB]` |
| `load-requests.sh` | `./load-requests.sh [N] [url]` |
| `network-interruption.sh` | `./network-interruption.sh [deploy] [seg] [namespace]` |
| `disconnect-component.sh` | `./disconnect-component.sh [deploy] [seg] [namespace]` |

Default deploy: pharmago-api-gateway

## Cobertura de caos

| Requisito | Script |
|-----------|--------|
| Inyeccion de requests | `load-requests.sh` |
| Sobrecarga de CPU | `cpu-spike.sh` |
| Sobrecarga de memoria | `ram-spike.sh` |
| Sobrecarga de storage | `volume-spike.sh` |
| Interrupcion de trafico de red | `network-interruption.sh` |
| Desconexion de componentes internos | `disconnect-component.sh` |

**Ver impacto en Grafana:** Dashboard "PharmaGo - Infra" → paneles "CPU por pod (pharmago)" y "Memoria por pod (pharmago)".

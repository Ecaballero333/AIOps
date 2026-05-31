#!/bin/bash
# Reconstruye/aplica componentes puntuales y reinicia sus workloads en Minikube.
# Uso:
#   ./deploy-target.sh                  # despliega todos los targets conocidos
#   ./deploy-target.sh gateway ui        # despliega solo esos componentes
#   ./deploy-target.sh grafana kibana    # reaplica config/manifiestos y reinicia

set -Eeuo pipefail

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
CODE_DIR="$(dirname "$SCRIPT_DIR")"
NAMESPACE="${NAMESPACE:-pharmago}"

STARTED_AT=$(date +%s)
CURRENT_STEP="Inicializando"

log() {
    echo "[$(date '+%H:%M:%S')] $*"
}

fail() {
    echo ""
    echo "ERROR: fallo en el paso: $CURRENT_STEP"
    echo "Revisa el mensaje anterior para ver el comando que falló."
}

trap fail ERR

usage() {
    cat <<EOF
Uso:
  ./deploy-target.sh [target...]

Si no se indican targets, despliega todos.

Targets disponibles:
  all             Todos los targets
  backend         gateway + users + pharmacy
  gateway         API Gateway
  users           Users Service
  pharmacy        Pharmacy Service
  ui | frontend   Frontend UI
  grafana         Grafana + configmaps de dashboards/provisioning
  kibana          Kibana
  prometheus      Prometheus + RBAC/configmap
  otel            OpenTelemetry Collector
  fluent-bit      Fluent Bit + RBAC/configmap
  elasticsearch   Elasticsearch
  db              SQL Server
  node-exporter   Node Exporter

Ejemplos:
  ./deploy-target.sh gateway
  ./deploy-target.sh ui grafana
  ./deploy-target.sh
EOF
}

require_cmd() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "Error: $1 no está instalado o no está en el PATH"
        exit 1
    fi
}

require_cluster() {
    CURRENT_STEP="Verificando kubectl y Minikube"
    require_cmd kubectl
    if ! minikube status >/dev/null 2>&1; then
        echo "Error: Minikube no está corriendo."
        echo "Ejecuta: minikube start --memory=6144 --cpus=4"
        exit 1
    fi
}

require_docker() {
    CURRENT_STEP="Verificando Docker"
    require_cmd docker
}

apply_file() {
    local file="$1"
    if [ -f "$SCRIPT_DIR/$file" ]; then
        log "Aplicando $file"
        kubectl apply -f "$SCRIPT_DIR/$file"
    else
        log "Omitiendo $file (no existe)"
    fi
}

rollout_deployment() {
    local deployment="$1"
    CURRENT_STEP="Reiniciando deployment/$deployment"
    log "Reiniciando deployment/$deployment"
    kubectl rollout restart "deployment/$deployment" -n "$NAMESPACE"

    CURRENT_STEP="Esperando rollout de deployment/$deployment"
    log "Esperando a que deployment/$deployment quede listo"
    kubectl rollout status "deployment/$deployment" -n "$NAMESPACE" --timeout=300s
}

rollout_daemonset() {
    local daemonset="$1"
    CURRENT_STEP="Reiniciando daemonset/$daemonset"
    log "Reiniciando daemonset/$daemonset"
    kubectl rollout restart "daemonset/$daemonset" -n "$NAMESPACE"

    CURRENT_STEP="Esperando rollout de daemonset/$daemonset"
    log "Esperando a que daemonset/$daemonset quede listo"
    kubectl rollout status "daemonset/$daemonset" -n "$NAMESPACE" --timeout=300s
}

build_backend_image() {
    local name="$1"
    local dockerfile="$2"

    require_docker
    CURRENT_STEP="Construyendo imagen $name"
    log "Construyendo imagen $name"
    ( cd "$CODE_DIR/Backend" && docker build -f "$dockerfile" -t "$name:latest" . )

    CURRENT_STEP="Cargando imagen $name en Minikube"
    log "Cargando $name:latest en Minikube"
    minikube image load "$name:latest"
}

build_frontend_image() {
    require_docker
    CURRENT_STEP="Construyendo imagen pharmago-ui"
    log "Construyendo imagen pharmago-ui"
    ( cd "$CODE_DIR/Frontend" && docker build -f Dockerfile -t pharmago-ui:latest . )

    CURRENT_STEP="Cargando imagen pharmago-ui en Minikube"
    log "Cargando pharmago-ui:latest en Minikube"
    minikube image load pharmago-ui:latest
}

deploy_gateway() {
    log "== Target: gateway =="
    build_backend_image "pharmago-api-gateway" "PharmaGo.ApiGateway/Dockerfile"
    apply_file "services/backend/api-gateway-service.yaml"
    apply_file "deployments/backend/api-gateway-deployment.yaml"
    rollout_deployment "pharmago-api-gateway"
}

deploy_users() {
    log "== Target: users =="
    build_backend_image "pharmago-users-service" "PharmaGo.UsersService/Dockerfile"
    apply_file "services/backend/users-service-service.yaml"
    apply_file "deployments/backend/users-service-deployment.yaml"
    rollout_deployment "pharmago-users-service"
}

deploy_pharmacy() {
    log "== Target: pharmacy =="
    build_backend_image "pharmago-pharmacy-service" "PharmaGo.PharmacyService/Dockerfile"
    apply_file "services/backend/pharmacy-service-service.yaml"
    apply_file "deployments/backend/pharmacy-service-deployment.yaml"
    rollout_deployment "pharmago-pharmacy-service"
}

deploy_ui() {
    log "== Target: ui =="
    build_frontend_image
    apply_file "services/frontend/ui-service.yaml"
    apply_file "deployments/frontend/ui-deployment.yaml"
    rollout_deployment "pharmago-ui"
}

deploy_db() {
    log "== Target: db =="
    apply_file "secrets/db-secret.yaml"
    apply_file "services/ops/db-service.yaml"
    apply_file "deployments/ops/db-deployment.yaml"
    rollout_deployment "pharmago-db"
}

deploy_elasticsearch() {
    log "== Target: elasticsearch =="
    apply_file "services/ops/elasticsearch-service.yaml"
    apply_file "deployments/ops/elasticsearch-deployment.yaml"
    rollout_deployment "elasticsearch"
}

deploy_otel() {
    log "== Target: otel =="
    apply_file "configmaps/otel-collector-config.yaml"
    apply_file "services/ops/otel-collector-service.yaml"
    apply_file "deployments/ops/otel-collector-deployment.yaml"
    rollout_deployment "otlp-collector"
}

deploy_prometheus() {
    log "== Target: prometheus =="
    apply_file "configmaps/prometheus-config.yaml"
    apply_file "deployments/ops/prometheus-serviceaccount.yaml"
    apply_file "deployments/ops/prometheus-clusterrole.yaml"
    apply_file "deployments/ops/prometheus-clusterrolebinding.yaml"
    apply_file "services/ops/prometheus-service.yaml"
    apply_file "deployments/ops/prometheus-deployment.yaml"
    rollout_deployment "prometheus"
}

deploy_node_exporter() {
    log "== Target: node-exporter =="
    apply_file "services/ops/node-exporter-service.yaml"
    apply_file "deployments/ops/node-exporter-daemonset.yaml"
    rollout_daemonset "node-exporter"
}

deploy_grafana() {
    log "== Target: grafana =="
    apply_file "configmaps/grafana-provisioning.yaml"
    apply_file "configmaps/grafana-dashboards.yaml"
    apply_file "configmaps/grafana-dashboard-infra.yaml"
    apply_file "configmaps/grafana-dashboard-business.yaml"
    apply_file "configmaps/grafana-alerts-business.yaml"
    apply_file "services/ops/grafana-service.yaml"
    apply_file "deployments/ops/grafana-deployment.yaml"
    rollout_deployment "grafana"
}

deploy_kibana() {
    log "== Target: kibana =="
    apply_file "services/ops/kibana-service.yaml"
    apply_file "deployments/ops/kibana-deployment.yaml"
    rollout_deployment "kibana"
}

deploy_fluent_bit() {
    log "== Target: fluent-bit =="
    apply_file "configmaps/fluent-bit-config.yaml"
    apply_file "deployments/ops/fluent-bit-serviceaccount.yaml"
    apply_file "deployments/ops/fluent-bit-clusterrole.yaml"
    apply_file "deployments/ops/fluent-bit-clusterrolebinding.yaml"
    apply_file "deployments/ops/fluent-bit-daemonset.yaml"
    rollout_daemonset "fluent-bit"
}

deploy_backend() {
    deploy_gateway
    deploy_users
    deploy_pharmacy
}

deploy_all() {
    deploy_db
    deploy_elasticsearch
    deploy_otel
    deploy_prometheus
    deploy_node_exporter
    deploy_grafana
    deploy_kibana
    deploy_fluent_bit
    deploy_backend
    deploy_ui
}

run_target() {
    case "$1" in
        all)
            deploy_all
            ;;
        backend)
            deploy_backend
            ;;
        gateway|api-gateway)
            deploy_gateway
            ;;
        users|users-service)
            deploy_users
            ;;
        pharmacy|pharmacy-service)
            deploy_pharmacy
            ;;
        ui|frontend)
            deploy_ui
            ;;
        db|database|sql)
            deploy_db
            ;;
        elasticsearch|elastic)
            deploy_elasticsearch
            ;;
        otel|otlp|otel-collector)
            deploy_otel
            ;;
        prometheus)
            deploy_prometheus
            ;;
        node-exporter)
            deploy_node_exporter
            ;;
        grafana)
            deploy_grafana
            ;;
        kibana)
            deploy_kibana
            ;;
        fluent-bit|fluentbit)
            deploy_fluent_bit
            ;;
        -h|--help|help)
            usage
            exit 0
            ;;
        *)
            echo "Target desconocido: $1"
            echo ""
            usage
            exit 1
            ;;
    esac
}

if [ "${1:-}" = "-h" ] || [ "${1:-}" = "--help" ] || [ "${1:-}" = "help" ]; then
    usage
    exit 0
fi

require_cluster

if [ "$#" -eq 0 ]; then
    TARGETS="all"
else
    TARGETS="$*"
fi

log "Iniciando despliegue. Namespace: $NAMESPACE. Targets: $TARGETS"
echo ""

if [ "$#" -eq 0 ]; then
    run_target all
else
    for target in "$@"; do
        run_target "$target"
    done
fi

FINISHED_AT=$(date +%s)
DURATION=$((FINISHED_AT - STARTED_AT))

echo ""
log "Despliegue finalizado correctamente en ${DURATION}s."
log "Estado actual de pods:"
kubectl get pods -n "$NAMESPACE" -o wide

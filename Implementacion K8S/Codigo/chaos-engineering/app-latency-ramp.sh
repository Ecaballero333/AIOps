#!/bin/sh
# Chaos: delay progresivo dentro de la app para que lo midan las metricas HTTP.
# Uso: ./app-latency-ramp.sh [deployment] [namespace] [seconds_per_step] [profile] [include_health]

DEPLOY="${1:-pharmago-pharmacy-service}"
NS="${2:-pharmago}"
STEP_SECONDS="${3:-15}"
PROFILE="${4:-500,1000,1500,2000,3000,4000,2500,1500,3000,1000,5000}"
INCLUDE_HEALTH="${5:-false}"
LATENCY_FILE="${LATENCY_FILE:-/tmp/pharmago-chaos-latency-ms}"
INCLUDE_HEALTH_FILE="${INCLUDE_HEALTH_FILE:-/tmp/pharmago-chaos-include-health}"

is_number() {
  case "$1" in
    ''|*[!0-9]*) return 1 ;;
    *) return 0 ;;
  esac
}

if ! is_number "$STEP_SECONDS" || [ "$STEP_SECONDS" -le 0 ]; then
  echo "Error: seconds_per_step debe ser un entero mayor a 0."
  exit 1
fi

PODS=$(kubectl get pod -n "$NS" \
  -l app="$DEPLOY" \
  --field-selector=status.phase=Running \
  -o jsonpath='{range .items[*]}{.metadata.name}{"\n"}{end}' 2>/dev/null)

if [ -z "$PODS" ]; then
  echo "Error: no hay pods Running con app=$DEPLOY en el namespace $NS."
  exit 1
fi

set_latency() {
  LATENCY_MS="$1"
  for POD in $PODS; do
    kubectl exec -n "$NS" "$POD" -- sh -c "printf '%s' '$LATENCY_MS' > '$LATENCY_FILE'"
    kubectl exec -n "$NS" "$POD" -- sh -c "printf '%s' '$INCLUDE_HEALTH' > '$INCLUDE_HEALTH_FILE'"
  done
}

restore_latency() {
  for POD in $PODS; do
    kubectl exec -n "$NS" "$POD" -- sh -c "rm -f '$LATENCY_FILE' '$INCLUDE_HEALTH_FILE'" >/dev/null 2>&1 || true
  done
}

cleanup_on_signal() {
  echo
  echo "Interrumpido. Restaurando latencia..."
  restore_latency
  exit 130
}

trap cleanup_on_signal INT TERM

echo "Target deployment=$DEPLOY namespace=$NS"
echo "Pods afectados:"
echo "$PODS"
echo
echo "Perfil de latencia: $PROFILE ms"
echo "Duracion por paso: ${STEP_SECONDS}s"
echo "Afecta /health: $INCLUDE_HEALTH"
echo
echo "Tip: mira Grafana -> PharmaGo - Overview -> Latencia Promedio por Endpoint."
echo

OLD_IFS="$IFS"
IFS=","
for LATENCY_MS in $PROFILE; do
  IFS="$OLD_IFS"
  LATENCY_MS=$(echo "$LATENCY_MS" | tr -d ' ')
  if ! is_number "$LATENCY_MS"; then
    echo "Error: valor de latencia invalido en profile: $LATENCY_MS"
    restore_latency
    exit 1
  fi

  echo "Aplicando delay app=${LATENCY_MS}ms durante ${STEP_SECONDS}s..."
  set_latency "$LATENCY_MS"
  sleep "$STEP_SECONDS"
  IFS=","
done
IFS="$OLD_IFS"

echo
printf "Restituir ahora la latencia de app en los pods? [s/N]: "
read ANSWER

case "$ANSWER" in
  s|S|si|SI|Si|y|Y|yes|YES|Yes)
    echo "Restaurando latencia..."
    restore_latency
    echo "Restaurado."
    ;;
  *)
    echo "No se restauro. Para restaurar manualmente:"
    for POD in $PODS; do
      echo "kubectl exec -n $NS $POD -- sh -c \"rm -f '$LATENCY_FILE' '$INCLUDE_HEALTH_FILE'\""
    done
    ;;
esac

#!/bin/sh
# Chaos: agrega latencia de red a un solo pod de pharmacy y pregunta cuando restaurar.
# Uso: ./pharmacy-network-latency.sh [delay_ms] [namespace] [deployment]

DELAY_MS="${1:-2000}"
NS="${2:-pharmago}"
DEPLOY="${3:-pharmago-pharmacy-service}"
IFACE="${IFACE:-eth0}"
WAIT_SECONDS="${WAIT_SECONDS:-40}"

is_number() {
  case "$1" in
    ''|*[!0-9]*) return 1 ;;
    *) return 0 ;;
  esac
}

if ! is_number "$DELAY_MS" || [ "$DELAY_MS" -le 0 ]; then
  echo "Error: delay_ms debe ser un entero mayor a 0."
  exit 1
fi

if ! is_number "$WAIT_SECONDS" || [ "$WAIT_SECONDS" -le 0 ]; then
  echo "Error: WAIT_SECONDS debe ser un entero mayor a 0."
  exit 1
fi

POD=$(kubectl get pod -n "$NS" \
  -l app="$DEPLOY" \
  --field-selector=status.phase=Running \
  -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)

if [ -z "$POD" ]; then
  echo "Error: no hay pods Running con app=$DEPLOY en el namespace $NS."
  exit 1
fi

echo "Target: pod=$POD namespace=$NS deployment=$DEPLOY iface=$IFACE delay=${DELAY_MS}ms"
echo
echo "Endpoints antes del caos:"
kubectl get endpoints -n "$NS" "$DEPLOY" -o wide
echo

if ! kubectl exec -n "$NS" "$POD" -- which tc >/dev/null 2>&1; then
  echo "Error: el pod no tiene 'tc'. Reconstrui la imagen de pharmacy con iproute2 y redeploya."
  echo "Sugerencia: k8s/deploy-target.sh pharmacy"
  exit 1
fi

if ! kubectl exec -n "$NS" "$POD" -- tc qdisc show dev "$IFACE" >/dev/null 2>&1; then
  echo "Error: no se puede consultar tc en $IFACE. Falta NET_ADMIN o la interfaz no existe."
  echo "Sugerencia: revisar securityContext.capabilities.add: [NET_ADMIN] en el Deployment."
  exit 1
fi

echo "Aplicando latencia de red a $POD..."
if ! kubectl exec -n "$NS" "$POD" -- tc qdisc replace dev "$IFACE" root netem delay "${DELAY_MS}ms"; then
  echo "Error: no se pudo aplicar netem. Revisa permisos NET_ADMIN."
  exit 1
fi

echo
echo "Latencia aplicada. Kubernetes deberia marcar el pod NotReady si /health supera timeoutSeconds."
echo "Esperando ${WAIT_SECONDS}s para que se vea en readiness/endpoints..."
sleep "$WAIT_SECONDS"

echo
echo "Pods:"
kubectl get pods -n "$NS" -l app="$DEPLOY" -o wide
echo
echo "Endpoints durante el caos:"
kubectl get endpoints -n "$NS" "$DEPLOY" -o wide
echo

printf "Restituir ahora la red del pod %s? [s/N]: " "$POD"
read ANSWER

case "$ANSWER" in
  s|S|si|SI|Si|y|Y|yes|YES|Yes)
    echo "Restaurando red de $POD..."
    kubectl exec -n "$NS" "$POD" -- tc qdisc del dev "$IFACE" root >/dev/null 2>&1 || true
    echo "Esperando ${WAIT_SECONDS}s para que readiness vuelva a estabilizar..."
    sleep "$WAIT_SECONDS"
    echo
    echo "Pods despues de restaurar:"
    kubectl get pods -n "$NS" -l app="$DEPLOY" -o wide
    echo
    echo "Endpoints despues de restaurar:"
    kubectl get endpoints -n "$NS" "$DEPLOY" -o wide
    ;;
  *)
    echo "No se restauro. El pod queda con latencia hasta que ejecutes:"
    echo "kubectl exec -n $NS $POD -- tc qdisc del dev $IFACE root"
    echo "o hasta que recrees el pod."
    ;;
esac

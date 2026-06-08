#!/bin/sh
# Chaos: desconectar un componente interno escalando su deployment a 0. Uso: ./disconnect-component.sh [deployment] [segundos] [namespace]
DEPLOY="${1:-pharmago-users-service}"
SECS="${2:-30}"
NS="${3:-pharmago}"

REPLICAS=$(kubectl get deployment -n "$NS" "$DEPLOY" -o jsonpath='{.spec.replicas}' 2>/dev/null)
if [ -z "$REPLICAS" ]; then
  echo "Error: no existe el deployment $DEPLOY en el namespace $NS."
  exit 1
fi

cleanup() {
  echo "Restaurando deployment/$DEPLOY a $REPLICAS replicas"
  kubectl scale deployment -n "$NS" "$DEPLOY" --replicas="$REPLICAS" >/dev/null 2>&1
}
trap cleanup EXIT INT TERM

echo "Disconnect component: deployment/$DEPLOY -> 0 replicas (${SECS}s)"
kubectl scale deployment -n "$NS" "$DEPLOY" --replicas=0
sleep "$SECS"

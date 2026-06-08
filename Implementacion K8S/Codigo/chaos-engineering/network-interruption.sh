#!/bin/sh
# Chaos: interrumpir trafico de red de un deployment. Uso: ./network-interruption.sh [deployment] [segundos] [namespace]
# Requiere que el cluster tenga un CNI que aplique NetworkPolicy.
DEPLOY="${1:-pharmago-api-gateway}"
SECS="${2:-30}"
NS="${3:-pharmago}"
POLICY="chaos-deny-network-$DEPLOY"

POD=$(kubectl get pod -n "$NS" -l app="$DEPLOY" -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)
if [ -z "$POD" ]; then
  echo "Error: no hay pods con app=$DEPLOY en el namespace $NS."
  exit 1
fi

cleanup() {
  kubectl delete networkpolicy -n "$NS" "$POLICY" --ignore-not-found >/dev/null 2>&1
}
trap cleanup EXIT INT TERM

echo "Network interruption: aislando app=$DEPLOY en namespace $NS (${SECS}s)"
kubectl apply -n "$NS" -f - <<EOF
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: $POLICY
spec:
  podSelector:
    matchLabels:
      app: $DEPLOY
  policyTypes:
  - Ingress
  - Egress
EOF

sleep "$SECS"
echo "Restaurando trafico de red para app=$DEPLOY"

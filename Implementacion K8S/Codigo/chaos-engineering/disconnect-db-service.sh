#!/bin/sh
# Chaos: desconectar la base de datos desde el Service, sin apagar el pod.
# Uso: ./disconnect-db-service.sh [service] [namespace] [app_label_original]
SERVICE="${1:-pharmago-db}"
NS="${2:-pharmago}"
CHAOS_SELECTOR="chaos-disconnected-$SERVICE"

CURRENT_APP=$(kubectl get service -n "$NS" "$SERVICE" -o jsonpath='{.spec.selector.app}' 2>/dev/null)
if [ -z "$CURRENT_APP" ]; then
  echo "Error: no existe el service $SERVICE en el namespace $NS, o no usa selector app=..."
  exit 1
fi

APP_LABEL="${3:-$CURRENT_APP}"

if [ "$CURRENT_APP" = "$CHAOS_SELECTOR" ]; then
  echo "Error: service/$SERVICE ya parece estar desconectado con app=$CHAOS_SELECTOR."
  echo "Restaurar manualmente: kubectl patch service -n $NS $SERVICE --type=merge -p '{\"spec\":{\"selector\":{\"app\":\"$APP_LABEL\"}}}'"
  exit 1
fi

cleanup() {
  echo "Restaurando service/$SERVICE selector app=$APP_LABEL"
  kubectl patch service -n "$NS" "$SERVICE" --type=merge -p "{\"spec\":{\"selector\":{\"app\":\"$APP_LABEL\"}}}" >/dev/null 2>&1
  kubectl get endpoints -n "$NS" "$SERVICE"
}
trap cleanup EXIT
trap 'exit 130' INT TERM

echo "DB service disconnect: service/$SERVICE selector app=$CURRENT_APP -> app=$CHAOS_SELECTOR"
echo "Endpoints antes del caos:"
kubectl get endpoints -n "$NS" "$SERVICE"

kubectl patch service -n "$NS" "$SERVICE" --type=merge -p "{\"spec\":{\"selector\":{\"app\":\"$CHAOS_SELECTOR\"}}}"

echo "Endpoints despues de desconectar:"
kubectl get endpoints -n "$NS" "$SERVICE"
echo "Durante la prueba, el pod de BD sigue corriendo pero service/$SERVICE queda sin endpoints."
echo "Probar login y luego un endpoint autenticado para ver login_db_lookup_fail/auth_db_lookup_fail."
echo
printf "Presionar Enter para reconectar service/%s al pod original..." "$SERVICE"

read _

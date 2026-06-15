#!/bin/sh
# Chaos: llenar disco/ephemeral en un pod aleatorio.
# Uso: ./volume-spike.sh [deployment] [total_mb] [chunk_mb] [sleep_seconds] [limit_mb] [pause_percent] [pause_seconds]
DEPLOY="${1:-pharmago-pharmacy-service}"
TOTAL_MB="${2:-140}"
CHUNK_MB="${3:-4}"
SLEEP_SECONDS="${4:-2}"
LIMIT_MB="${5:-128}"
PAUSE_PERCENT="${6:-95}"
PAUSE_SECONDS="${7:-45}"
NAMESPACE="${NAMESPACE:-pharmago}"

PODS=$(kubectl get pod -n "$NAMESPACE" -l app="$DEPLOY" \
  --field-selector=status.phase=Running \
  -o jsonpath='{range .items[*]}{.metadata.name}{"\n"}{end}' 2>/dev/null)

POD=$(printf '%s\n' "$PODS" | awk 'NF { pods[++count]=$0 } END { if (count > 0) { srand(); print pods[int(rand() * count) + 1] } }')

if [ -z "$POD" ]; then
  echo "Error: no hay pods Running con app=$DEPLOY en el namespace $NAMESPACE."
  exit 1
fi

case "$TOTAL_MB:$CHUNK_MB:$SLEEP_SECONDS:$LIMIT_MB:$PAUSE_PERCENT:$PAUSE_SECONDS" in
  *[!0-9:]*)
    echo "Error: total_mb, chunk_mb, sleep_seconds, limit_mb, pause_percent y pause_seconds deben ser numeros enteros."
    exit 1
    ;;
esac

if [ "$TOTAL_MB" -le 0 ] || [ "$CHUNK_MB" -le 0 ] || [ "$LIMIT_MB" -le 0 ]; then
  echo "Error: total_mb, chunk_mb y limit_mb deben ser mayores a 0."
  exit 1
fi

PAUSE_AT_MB=$(((LIMIT_MB * PAUSE_PERCENT + 99) / 100))
PAUSE_DONE=0

echo "Volume spike: pod=$POD deployment=$DEPLOY namespace=$NAMESPACE total=${TOTAL_MB}Mi chunk=${CHUNK_MB}Mi sleep=${SLEEP_SECONDS}s limit=${LIMIT_MB}Mi pause_at=${PAUSE_AT_MB}Mi (${PAUSE_PERCENT}%) pause=${PAUSE_SECONDS}s"
kubectl exec -n "$NAMESPACE" "$POD" -- sh -c "df -h /tmp || true"

WRITTEN=0
INDEX=1
while [ "$WRITTEN" -lt "$TOTAL_MB" ]; do
  REMAINING=$((TOTAL_MB - WRITTEN))
  if [ "$REMAINING" -lt "$CHUNK_MB" ]; then
    CURRENT_MB="$REMAINING"
  else
    CURRENT_MB="$CHUNK_MB"
  fi

  TARGET="/tmp/chaos_fill_$INDEX"
  echo "Writing ${CURRENT_MB}Mi to $TARGET (${WRITTEN}/${TOTAL_MB}Mi written before this chunk)"

  if ! kubectl exec -n "$NAMESPACE" "$POD" -- sh -c "dd if=/dev/zero of='$TARGET' bs=1M count=$CURRENT_MB conv=fsync"; then
    echo "Write failed. The pod may have exceeded ephemeral-storage or become unavailable."
    kubectl get pod -n "$NAMESPACE" "$POD" 2>/dev/null || true
    exit 1
  fi

  WRITTEN=$((WRITTEN + CURRENT_MB))
  INDEX=$((INDEX + 1))

  if [ "$PAUSE_DONE" -eq 0 ] && [ "$WRITTEN" -ge "$PAUSE_AT_MB" ]; then
    PAUSE_DONE=1
    if [ "$PAUSE_SECONDS" -gt 0 ]; then
      echo "Reached ${WRITTEN}Mi, at or above ${PAUSE_PERCENT}% of ${LIMIT_MB}Mi. Sleeping ${PAUSE_SECONDS}s before writing more."
      sleep "$PAUSE_SECONDS"
    fi
  fi

  kubectl exec -n "$NAMESPACE" "$POD" -- sh -c "df -h /tmp || true" || {
    echo "The pod stopped responding after writing ${WRITTEN}Mi."
    kubectl get pod -n "$NAMESPACE" "$POD" 2>/dev/null || true
    exit 1
  }

  if [ "$WRITTEN" -lt "$TOTAL_MB" ] && [ "$SLEEP_SECONDS" -gt 0 ]; then
    sleep "$SLEEP_SECONDS"
  fi
done

echo "Volume spike finished: wrote ${WRITTEN}Mi into $POD."

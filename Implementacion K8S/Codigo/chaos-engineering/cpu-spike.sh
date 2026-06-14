#!/bin/sh
# Chaos: CPU spike interactivo en pods PharmaGo.
# Mantiene carga controlada hasta cortar el script. Al salir, mata los loops remotos.

set -u

NAMESPACE="${PHARMAGO_NAMESPACE:-pharmago}"
SCOPE="${PHARMAGO_CPU_SCOPE:-menu}"
TARGET="${PHARMAGO_CPU_TARGET:-}"
INTENSITY="${PHARMAGO_CPU_INTENSITY:-medium}"
PID_FILE="/tmp/pharmago-cpu-spike.pids"
MANAGED_PODS_FILE="/tmp/pharmago-cpu-spike-managed-pods.$$"

usage() {
  cat <<USAGE
Uso:
  ./cpu-spike.sh [opciones]

Opciones:
  -n, --namespace NS        Namespace. Default: $NAMESPACE
  -s, --scope SCOPE         menu|all|pod|deploy. Default: $SCOPE
  -t, --target TARGET       Pod exacto o deployment/app, segun scope
  -i, --intensity LEVEL     low|medium|high. Default: $INTENSITY
  -h, --help                Muestra esta ayuda

Modos:
  all     Ataca todos los pods de servicios PharmaGo en el namespace.
  pod     Ataca una instancia/pod exacto. Copiar y pegar nombre del pod.
  deploy  Ataca todos los pods de un deployment/app label.

Intensidad objetivo:
  low     ~50% de 1 core.
  medium  ~75% de 1 core.
  high    ~95% de 1 core. Busca superar 90%.

Importante:
  Para ver 75% o 90% en el dashboard actual, el pod necesita limit.cpu >= 1000m.
  Con limit.cpu=500m, Kubernetes limita el contenedor cerca de 50%.

Ejemplos:
  ./cpu-spike.sh
  ./cpu-spike.sh --scope all --intensity high
  ./cpu-spike.sh --scope pod --target pharmago-api-gateway-xxxxx --intensity medium
  ./cpu-spike.sh --scope deploy --target pharmago-users-service --intensity low

Detener:
  Ctrl+C. El script ejecuta cleanup y mata los loops remotos que creo.
USAGE
}

target_for_intensity() {
  case "$INTENSITY" in
    low) echo "500 0.500 50" ;;
    medium) echo "750 0.250 75" ;;
    high) echo "950 0.050 95" ;;
    *) echo "Intensidad invalida: $INTENSITY" >&2; exit 2 ;;
  esac
}

list_service_pods() {
  kubectl get pods -n "$NAMESPACE" \
    -l 'app in (pharmago-api-gateway,pharmago-users-service,pharmago-pharmacy-service)' \
    -o jsonpath='{range .items[*]}{.metadata.name}{"\n"}{end}' 2>/dev/null
}

list_deploy_pods() {
  app="$1"
  kubectl get pods -n "$NAMESPACE" -l "app=$app" \
    -o jsonpath='{range .items[*]}{.metadata.name}{"\n"}{end}' 2>/dev/null
}

show_pods() {
  echo "Pods disponibles en namespace $NAMESPACE:"
  kubectl get pods -n "$NAMESPACE" -o custom-columns='NAME:.metadata.name,APP:.metadata.labels.app,STATUS:.status.phase' 2>/dev/null || true
}

menu() {
  echo "PharmaGo CPU Spike Chaos"
  echo "1) Atacar todas las instancias de todos los servicios"
  echo "2) Atacar una instancia particular (copiar y pegar pod)"
  echo "3) Atacar todas las instancias de un servicio/deployment"
  printf 'Seleccione alcance [1-3]: '
  read choice
  case "$choice" in
    1) SCOPE=all ;;
    2)
      SCOPE=pod
      show_pods
      printf 'Pegue el nombre exacto del pod: '
      read TARGET
      ;;
    3)
      SCOPE=deploy
      echo "Servicios comunes: pharmago-api-gateway, pharmago-users-service, pharmago-pharmacy-service"
      printf 'Ingrese app/deployment: '
      read TARGET
      ;;
    *) echo "Opcion invalida" >&2; exit 2 ;;
  esac

  echo "Intensidad: low (~50%), medium (~75%), high (~95%)"
  printf 'Seleccione intensidad [%s]: ' "$INTENSITY"
  read input
  [ -n "$input" ] && INTENSITY="$input"
}

pods_for_scope() {
  case "$SCOPE" in
    all)
      list_service_pods
      ;;
    pod)
      if [ -z "$TARGET" ]; then
        echo "Scope pod requiere --target o seleccion por menu." >&2
        exit 2
      fi
      printf '%s\n' "$TARGET"
      ;;
    deploy)
      if [ -z "$TARGET" ]; then
        echo "Scope deploy requiere --target." >&2
        exit 2
      fi
      list_deploy_pods "$TARGET"
      ;;
    *)
      echo "Scope invalido: $SCOPE" >&2
      exit 2
      ;;
  esac
}

start_spike_in_pod() {
  pod="$1"
  busy_ms="$2"
  sleep_seconds="$3"
  target_percent="$4"

  echo "Iniciando CPU spike en $pod objetivo ~$target_percent%..."
  kubectl exec -n "$NAMESPACE" "$pod" -- sh -c '
    pid_file="'"$PID_FILE"'"
    busy_ms='"$busy_ms"'
    sleep_seconds="'"$sleep_seconds"'"

    now_ms() {
      ns=$(date +%s%N 2>/dev/null || echo "")
      case "$ns" in
        ""|*N*) echo $(($(date +%s) * 1000)) ;;
        *) echo $((ns / 1000000)) ;;
      esac
    }

    if [ -f "$pid_file" ]; then
      while read old_pid; do
        kill "$old_pid" 2>/dev/null || true
      done < "$pid_file"
      rm -f "$pid_file"
    fi

    (
      while :; do
        deadline=$(( $(now_ms) + busy_ms ))
        while [ "$(now_ms)" -lt "$deadline" ]; do
          :
        done
        sleep "$sleep_seconds"
      done
    ) &

    echo $! > "$pid_file"
    echo "started controlled cpu loop pid $(cat "$pid_file")"
  ' >/dev/null

  printf '%s\n' "$pod" >> "$MANAGED_PODS_FILE"
}

cleanup_pod() {
  pod="$1"
  echo "Limpiando CPU spike en $pod..."
  kubectl exec -n "$NAMESPACE" "$pod" -- sh -c '
    pid_file="'"$PID_FILE"'"
    if [ -f "$pid_file" ]; then
      while read pid; do
        kill "$pid" 2>/dev/null || true
      done < "$pid_file"
      rm -f "$pid_file"
    fi
  ' >/dev/null 2>&1 || true
}

CLEANED_UP=0

cleanup() {
  if [ "$CLEANED_UP" = "1" ]; then
    return
  fi
  CLEANED_UP=1
  echo ""
  echo "Deteniendo CPU spike..."
  if [ -f "$MANAGED_PODS_FILE" ]; then
    sort -u "$MANAGED_PODS_FILE" | while read pod; do
      [ -n "$pod" ] && cleanup_pod "$pod"
    done
    rm -f "$MANAGED_PODS_FILE"
  fi
  echo "Cleanup terminado."
}

wait_forever() {
  echo "CPU spike activo. Presiona Ctrl+C para detener y limpiar."
  while :; do
    sleep 1
  done
}

while [ $# -gt 0 ]; do
  case "$1" in
    -n|--namespace) NAMESPACE="$2"; shift 2 ;;
    -s|--scope) SCOPE="$2"; shift 2 ;;
    -t|--target) TARGET="$2"; shift 2 ;;
    -i|--intensity) INTENSITY="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Argumento desconocido: $1" >&2; usage; exit 2 ;;
  esac
done

if [ "$SCOPE" = "menu" ]; then
  menu
fi

set -- $(target_for_intensity)
busy_ms="$1"
sleep_seconds="$2"
target_percent="$3"
pods=$(pods_for_scope | sed '/^$/d')

if [ -z "$pods" ]; then
  echo "No encontre pods para scope=$SCOPE target=$TARGET namespace=$NAMESPACE" >&2
  show_pods
  exit 1
fi

: > "$MANAGED_PODS_FILE"
trap cleanup INT TERM EXIT

printf '%s\n' "$pods" | while read pod; do
  [ -n "$pod" ] && start_spike_in_pod "$pod" "$busy_ms" "$sleep_seconds" "$target_percent"
done

wait_forever

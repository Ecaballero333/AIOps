#!/bin/sh
# Chaos: RAM spike interactivo en pods PharmaGo.
# Mantiene memoria reservada hasta cortar el script. Al salir, mata los procesos remotos.

set -u

NAMESPACE="${PHARMAGO_NAMESPACE:-pharmago}"
SCOPE="${PHARMAGO_RAM_SCOPE:-menu}"
TARGET="${PHARMAGO_RAM_TARGET:-}"
INTENSITY="${PHARMAGO_RAM_INTENSITY:-medium}"
SAFETY_MB="${PHARMAGO_RAM_SAFETY_MB:-8}"
PID_FILE="/tmp/pharmago-ram-spike.pids"
MANAGED_PODS_FILE="/tmp/pharmago-ram-spike-managed-pods.$$"

usage() {
  cat <<USAGE
Uso:
  ./ram-spike.sh [opciones]

Opciones:
  -n, --namespace NS        Namespace. Default: $NAMESPACE
  -s, --scope SCOPE         menu|all|pod|deploy. Default: $SCOPE
  -t, --target TARGET       Pod exacto o deployment/app, segun scope
  -i, --intensity LEVEL     low|medium|high|oom. Default: $INTENSITY
  --safety-mb MB            Margen bajo el objetivo para evitar OOM. Default: $SAFETY_MB
  -h, --help                Muestra esta ayuda

Modos:
  all     Ataca todos los pods de servicios PharmaGo en el namespace.
  pod     Ataca una instancia/pod exacto. Copiar y pegar nombre del pod.
  deploy  Ataca todos los pods de un deployment/app label.

Intensidad objetivo:
  low     Intenta llevar cada pod a ~50% de su limite de memoria.
  medium  Intenta llevar cada pod a ~75% de su limite de memoria.
  high    Intenta llevar cada pod a ~86% de su limite de memoria para superar la alerta sin tocar el limite.
  oom     Intenta superar el limite de memoria para provocar OOMKilled.

Importante:
  La alerta de memoria por servicio evalua consumo total del servicio / limite total del servicio.
  Para disparar PharmaGo - Memoria alta por servicio > 85%, usa high sobre todas las instancias del servicio.
  Si hay OOMKilled en low/medium/high, aumenta --safety-mb. Si no supera 85%, baja --safety-mb con cuidado.
  En intensidad oom, OOMKilled es el resultado esperado.

Ejemplos:
  ./ram-spike.sh
  ./ram-spike.sh --scope all --intensity high
  ./ram-spike.sh --scope pod --target pharmago-api-gateway-xxxxx --intensity medium
  ./ram-spike.sh --scope deploy --target pharmago-users-service --intensity high
  ./ram-spike.sh --scope deploy --target pharmago-api-gateway --intensity high --safety-mb 16
  ./ram-spike.sh --scope pod --target pharmago-api-gateway-xxxxx --intensity oom

Detener:
  Ctrl+C. El script ejecuta cleanup y libera la memoria reservada en los pods.
USAGE
}

target_for_intensity() {
  case "$INTENSITY" in
    low) echo 50 ;;
    medium) echo 75 ;;
    high) echo 86 ;;
    oom) echo 110 ;;
    *) echo "Intensidad invalida: $INTENSITY" >&2; exit 2 ;;
  esac
}

list_service_pods() {
  kubectl get pods -n "$NAMESPACE" \
    -l 'app in (pharmago-api-gateway,pharmago-users-service,pharmago-pharmacy-service,pharmago-ui)' \
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
  echo "PharmaGo RAM Spike Chaos"
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
      echo "Servicios comunes: pharmago-api-gateway, pharmago-users-service, pharmago-pharmacy-service, pharmago-ui"
      printf 'Ingrese app/deployment: '
      read TARGET
      ;;
    *) echo "Opcion invalida" >&2; exit 2 ;;
  esac

  echo "Intensidad: low (~50%), medium (~75%), high (~86%), oom (provoca OOMKilled)"
  printf 'Seleccione intensidad [%s]: ' "$INTENSITY"
  read input
  [ -n "$input" ] && INTENSITY="$input"

  printf 'Margen anti-OOM en MiB [%s]: ' "$SAFETY_MB"
  read input
  [ -n "$input" ] && SAFETY_MB="$input"
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

validate_number() {
  value="$1"
  name="$2"
  case "$value" in
    ''|*[!0-9]*) echo "$name debe ser numerico: $value" >&2; exit 2 ;;
  esac
}

start_spike_in_pod() {
  pod="$1"
  target_percent="$2"

  echo "Iniciando RAM spike en $pod objetivo ~$target_percent% del limite..."
  kubectl exec -n "$NAMESPACE" "$pod" -- sh -c '
    pid_file="'"$PID_FILE"'"
    target_percent='"$target_percent"'
    safety_mb='"$SAFETY_MB"'

    if ! command -v perl >/dev/null 2>&1; then
      echo "El contenedor no tiene perl; no puedo reservar memoria de forma estable" >&2
      exit 1
    fi

    read_limit_bytes() {
      if [ -f /sys/fs/cgroup/memory.max ]; then
        limit=$(cat /sys/fs/cgroup/memory.max 2>/dev/null || echo "")
        [ "$limit" != "max" ] && [ -n "$limit" ] && echo "$limit" && return
      fi
      if [ -f /sys/fs/cgroup/memory/memory.limit_in_bytes ]; then
        cat /sys/fs/cgroup/memory/memory.limit_in_bytes 2>/dev/null && return
      fi
      echo 0
    }

    read_usage_bytes() {
      if [ -f /sys/fs/cgroup/memory.current ]; then
        cat /sys/fs/cgroup/memory.current 2>/dev/null && return
      fi
      if [ -f /sys/fs/cgroup/memory/memory.usage_in_bytes ]; then
        cat /sys/fs/cgroup/memory/memory.usage_in_bytes 2>/dev/null && return
      fi
      echo 0
    }

    cleanup_existing() {
      if [ -f "$pid_file" ]; then
        while read old_pid; do
          kill "$old_pid" 2>/dev/null || true
        done < "$pid_file"
        rm -f "$pid_file"
      fi
    }

    cleanup_existing

    limit_bytes=$(read_limit_bytes)
    usage_bytes=$(read_usage_bytes)

    case "$limit_bytes" in
      ""|0|*[!0-9]*)
        echo "No pude detectar limite de memoria del pod" >&2
        exit 1
        ;;
    esac

    one_mb=$((1024 * 1024))
    target_bytes=$((limit_bytes * target_percent / 100))
    alloc_bytes=$((target_bytes - usage_bytes))
    alloc_mb=$((alloc_bytes / one_mb - safety_mb))

    if [ "$alloc_mb" -lt 1 ]; then
      alloc_mb=1
    fi

    if [ "$target_percent" -gt 100 ]; then
      echo "modo oom: relanza rampas de memoria hasta que cortes el script"
      nohup sh -c '\''
        while :; do
          perl -e "my \$chunk_mb = shift || 4; my \$sleep_us = shift || 200000; my @chunks; \$SIG{TERM} = sub { exit 0 }; \$SIG{INT} = sub { exit 0 }; while (1) { push @chunks, \"X\" x (\$chunk_mb * 1024 * 1024); select undef, undef, undef, \$sleep_us / 1_000_000; }" 4 200000
          echo "memory ramp finished or was killed; restarting" >&2
          sleep 1
        done
      '\'' >/tmp/pharmago-ram-spike.log 2>&1 &
    else
      echo "limit=${limit_bytes}B usage=${usage_bytes}B target=${target_percent}% safety=${safety_mb}MiB allocate=${alloc_mb}MiB"
      nohup perl -e '\''
        my $mb = shift;
        my $bytes = $mb * 1024 * 1024;
        my $blob = "X" x $bytes;
        $SIG{TERM} = sub { exit 0 };
        $SIG{INT} = sub { exit 0 };
        sleep 3600 while 1;
      '\'' "$alloc_mb" >/tmp/pharmago-ram-spike.log 2>&1 &
    fi
    echo $! > "$pid_file"

    echo "started memory holder pid $(cat "$pid_file")"
  '

  printf '%s\n' "$pod" >> "$MANAGED_PODS_FILE"
}

cleanup_pod() {
  pod="$1"
  echo "Limpiando RAM spike en $pod..."
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
  echo "Deteniendo RAM spike..."
  if [ -f "$MANAGED_PODS_FILE" ]; then
    sort -u "$MANAGED_PODS_FILE" | while read pod; do
      [ -n "$pod" ] && cleanup_pod "$pod"
    done
    rm -f "$MANAGED_PODS_FILE"
  fi
  echo "Cleanup terminado."
}

wait_forever() {
  echo "RAM spike activo. Presiona Ctrl+C para detener y limpiar."
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
    --safety-mb) SAFETY_MB="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Argumento desconocido: $1" >&2; usage; exit 2 ;;
  esac
done

validate_number "$SAFETY_MB" "--safety-mb"

if [ "$SCOPE" = "menu" ]; then
  menu
  validate_number "$SAFETY_MB" "--safety-mb"
fi

target_percent=$(target_for_intensity)
pods=$(pods_for_scope | sed '/^$/d')

if [ -z "$pods" ]; then
  echo "No encontre pods para scope=$SCOPE target=$TARGET namespace=$NAMESPACE" >&2
  show_pods
  exit 1
fi

: > "$MANAGED_PODS_FILE"
trap cleanup INT TERM EXIT

printf '%s\n' "$pods" | while read pod; do
  [ -n "$pod" ] && start_spike_in_pod "$pod" "$target_percent"
done

wait_forever

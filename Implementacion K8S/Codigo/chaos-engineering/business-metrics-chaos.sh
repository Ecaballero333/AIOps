#!/bin/sh
# Chaos: dispara metricas de negocio de PharmaGo - Business.
# Requiere port-forward al API Gateway, por defecto http://127.0.0.1:5000.

set -u

BASE_URL="${PHARMAGO_BASE_URL:-http://127.0.0.1:5000}"
COUNT="${PHARMAGO_CHAOS_COUNT:-50}"
DELAY_MS="${PHARMAGO_CHAOS_DELAY_MS:-100}"
FUNCTIONALITY="${PHARMAGO_CHAOS_FUNCTIONALITY:-menu}"
SHOULD_ERROR="${PHARMAGO_CHAOS_ERROR:-mixed}"
USERNAME="${PHARMAGO_USERNAME:-}"
PASSWORD="${PHARMAGO_PASSWORD:-}"
BAD_USERNAME="${PHARMAGO_BAD_USERNAME:-chaos-invalid-user}"
BAD_PASSWORD="${PHARMAGO_BAD_PASSWORD:-chaos-invalid-password}"
TOKEN="${PHARMAGO_TOKEN:-}"
PURCHASE_ID="${PHARMAGO_PURCHASE_ID:-}"
PHARMACY_ID="${PHARMAGO_PHARMACY_ID:-}"
DRUG_CODE="${PHARMAGO_DRUG_CODE:-}"
LAST_BODY_FILE="/tmp/pharmago-chaos-last-body.$$"
PURCHASE_EMAIL="${PHARMAGO_PURCHASE_EMAIL:-chaos.purchase@example.com}"
VERBOSE="${PHARMAGO_CHAOS_VERBOSE:-0}"

usage() {
  cat <<USAGE
Uso:
  ./business-metrics-chaos.sh [opciones]

Opciones:
  -u, --url URL                 Base URL del API Gateway. Default: $BASE_URL
  -f, --functionality FUNC      menu|login|purchase-create|purchase-approve|purchase-reject|purchase-status|all
  -e, --error MODE              yes|no|mixed. Default: $SHOULD_ERROR
  -n, --count N                 Cantidad de requests. Default: $COUNT
  -d, --delay-ms MS             Demora entre requests en ms. Default: $DELAY_MS
      --username USER           Usuario para login exitoso. Tambien PHARMAGO_USERNAME
      --password PASS           Password para login exitoso. Tambien PHARMAGO_PASSWORD
      --bad-username USER       Usuario invalido. Default: $BAD_USERNAME
      --bad-password PASS       Password invalida. Default: $BAD_PASSWORD
      --token TOKEN             Token existente para endpoints autenticados. Tambien PHARMAGO_TOKEN
      --purchase-id ID          Id de compra para approve/reject. Si no se indica, se crea una compra nueva
      --pharmacy-id ID          Pharmacy id para compras/status. Si no se indica, se descubre desde /api/drug
      --drug-code CODE          Codigo de droga para compras/status. Si no se indica, se descubre desde /api/drug
      --purchase-email EMAIL    Email usado en create purchase. Default: $PURCHASE_EMAIL
  -v, --verbose                 Imprime respuesta resumida de cada request
  -h, --help                    Muestra esta ayuda

Ejemplos:
  ./business-metrics-chaos.sh
  ./business-metrics-chaos.sh -f login -e yes -n 200 -d 20
  ./business-metrics-chaos.sh -f purchase-create -e no -n 30
  PHARMAGO_USERNAME=Juan PHARMAGO_PASSWORD=123456 ./business-metrics-chaos.sh -f all -e mixed -n 100

Metricas que mueve:
  pharmago_business_events_total: login, purchase_create, purchase_status_change
  pharmago_login_attempts_total: success/failed
  pharmago_purchases_created_total
  pharmago_purchase_amount_total
  pharmago_purchase_items_total
  pharmago_purchase_status_changes_total

Nota: success de login y status changes requieren credenciales/token validos.
USAGE
}

json_escape() {
  printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

sleep_delay() {
  if [ "$DELAY_MS" -gt 0 ] 2>/dev/null; then
    sleep "$(awk "BEGIN { printf \"%.3f\", $DELAY_MS / 1000 }")"
  fi
}

request() {
  method="$1"
  path="$2"
  body="${3:-}"
  auth="${4:-}"

  headers_file="/tmp/pharmago-chaos-headers.$$"
  body_file="/tmp/pharmago-chaos-body.$$"

  if [ -n "$auth" ]; then
    code=$(curl -sS -o "$body_file" -D "$headers_file" -w '%{http_code}' -X "$method" "$BASE_URL$path" \
      -H 'Content-Type: application/json' \
      -H "Authorization: $auth" \
      -d "$body" 2>/dev/null || printf '000')
  elif [ -n "$body" ]; then
    code=$(curl -sS -o "$body_file" -D "$headers_file" -w '%{http_code}' -X "$method" "$BASE_URL$path" \
      -H 'Content-Type: application/json' \
      -d "$body" 2>/dev/null || printf '000')
  else
    code=$(curl -sS -o "$body_file" -D "$headers_file" -w '%{http_code}' -X "$method" "$BASE_URL$path" 2>/dev/null || printf '000')
  fi

  if [ "$VERBOSE" = "1" ]; then
    {
      printf '%s %s%s -> %s ' "$method" "$BASE_URL" "$path" "$code"
      head -c 180 "$body_file" | tr '\n' ' '
      printf '\n'
    } >&2
  fi

  cp "$body_file" "$LAST_BODY_FILE" 2>/dev/null || true
  rm -f "$headers_file" "$body_file"
  printf '%s' "$code"
}

login_body() {
  user=$(json_escape "$1")
  pass=$(json_escape "$2")
  printf '{"userName":"%s","password":"%s"}' "$user" "$pass"
}

purchase_body_success() {
  email=$(json_escape "$PURCHASE_EMAIL")
  code=$(json_escape "$DRUG_CODE")
  now=$(date -u '+%Y-%m-%dT%H:%M:%SZ')
  printf '{"buyerEmail":"%s","purchaseDate":"%s","details":[{"pharmacyId":%s,"code":"%s","quantity":1}]}' "$email" "$now" "$PHARMACY_ID" "$code"
}

purchase_body_error() {
  now=$(date -u '+%Y-%m-%dT%H:%M:%SZ')
  printf '{"buyerEmail":"invalid-email","purchaseDate":"%s","details":[{"pharmacyId":999999,"code":"CHAOS-NOT-FOUND","quantity":-1}]}' "$now"
}

status_body_success() {
  code=$(json_escape "$DRUG_CODE")
  printf '{"pharmacyId":%s,"drugCode":"%s"}' "$PHARMACY_ID" "$code"
}

status_body_error() {
  printf '{"pharmacyId":999999,"drugCode":"CHAOS-NOT-FOUND"}'
}

extract_token() {
  # Extrae un GUID del JSON de login sin depender de jq.
  printf '%s' "$1" | sed -n 's/.*"token"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p'
}

json_get_number() {
  key="$1"
  python3 -c 'import json,sys; key=sys.argv[1]; data=json.load(sys.stdin); value=data.get(key, ""); print(value if value is not None else "")' "$key" 2>/dev/null
}

json_drug_candidates() {
  python3 -c '
import json, sys
try:
    data = json.load(sys.stdin)
except Exception:
    sys.exit(1)
for drug in data if isinstance(data, list) else []:
    drug_id = drug.get("id") or drug.get("Id")
    code = drug.get("code") or drug.get("Code")
    pharmacy = drug.get("pharmacy") or drug.get("Pharmacy") or {}
    pharmacy_id = pharmacy.get("id") or pharmacy.get("Id")
    if drug_id and code and pharmacy_id:
        print(f"{drug_id} {pharmacy_id} {code}")
' 2>/dev/null
}

json_purchase_item_from_drug_detail() {
  python3 -c '
import json, sys
try:
    drug = json.load(sys.stdin)
except Exception:
    sys.exit(1)
code = drug.get("code") or drug.get("Code")
stock = drug.get("stock") or drug.get("Stock") or 0
pharmacy = drug.get("pharmacy") or drug.get("Pharmacy") or {}
pharmacy_id = pharmacy.get("id") or pharmacy.get("Id")
try:
    stock = int(stock)
except Exception:
    stock = 0
if code and pharmacy_id and stock > 0:
    print(f"{pharmacy_id} {code}")
    sys.exit(0)
sys.exit(1)
' 2>/dev/null
}

ensure_purchase_item() {
  if [ -n "$PHARMACY_ID" ] && [ -n "$DRUG_CODE" ]; then
    return 0
  fi

  request GET /api/drug "" "" >/dev/null
  if [ ! -s "$LAST_BODY_FILE" ]; then
    echo "No pude descubrir droga/farmacia: /api/drug no devolvio cuerpo." >&2
    return 1
  fi

  candidates=$(cat "$LAST_BODY_FILE" | json_drug_candidates || true)
  item=""
  printf '%s\n' "$candidates" | while read drug_id candidate_pharmacy_id candidate_code; do
    [ -z "$drug_id" ] && continue
    request GET "/api/drug/$drug_id" "" "" >/dev/null
    detail_item=$(cat "$LAST_BODY_FILE" | json_purchase_item_from_drug_detail || true)
    if [ -n "$detail_item" ]; then
      printf '%s' "$detail_item" > "/tmp/pharmago-chaos-item.$$"
      break
    fi
  done

  if [ -f "/tmp/pharmago-chaos-item.$$" ]; then
    item=$(cat "/tmp/pharmago-chaos-item.$$")
    rm -f "/tmp/pharmago-chaos-item.$$"
  fi

  if [ -z "$item" ]; then
    echo "No pude descubrir una droga con stock desde /api/drug. Usa --pharmacy-id y --drug-code." >&2
    return 1
  fi

  PHARMACY_ID=$(printf '%s' "$item" | awk '{print $1}')
  DRUG_CODE=$(printf '%s' "$item" | awk '{print $2}')
  [ "$VERBOSE" = "1" ] && echo "Usando pharmacy_id=$PHARMACY_ID drug_code=$DRUG_CODE" >&2
  return 0
}

create_valid_purchase() {
  if ! ensure_purchase_item; then
    return 1
  fi

  code=$(request POST /api/purchases "$(purchase_body_success)" "")
  purchase_id=""
  if [ -s "$LAST_BODY_FILE" ]; then
    purchase_id=$(cat "$LAST_BODY_FILE" | json_get_number Id)
    [ -z "$purchase_id" ] && purchase_id=$(cat "$LAST_BODY_FILE" | json_get_number id)
  fi

  case "$code" in
    2*)
      if [ -n "$purchase_id" ]; then
        LAST_PURCHASE_ID="$purchase_id"
        return 0
      fi
      echo "La compra se creo pero no pude extraer Id de la respuesta." >&2
      return 1
      ;;
    *)
      echo "No pude crear compra valida para status change. HTTP $code." >&2
      return 1
      ;;
  esac
}

ensure_token() {
  if [ -n "$TOKEN" ]; then
    return 0
  fi

  if [ -z "$USERNAME" ] || [ -z "$PASSWORD" ]; then
    return 1
  fi

  body_file="/tmp/pharmago-login-body.$$"
  code=$(curl -sS -o "$body_file" -w '%{http_code}' -X POST "$BASE_URL/api/login" \
    -H 'Content-Type: application/json' \
    -d "$(login_body "$USERNAME" "$PASSWORD")" 2>/dev/null || printf '000')
  response=$(cat "$body_file")
  rm -f "$body_file"

  if [ "$code" = "200" ]; then
    TOKEN=$(extract_token "$response")
    [ -n "$TOKEN" ] && return 0
  fi

  return 1
}

should_fail_iteration() {
  i="$1"
  case "$SHOULD_ERROR" in
    yes) return 0 ;;
    no) return 1 ;;
    mixed)
      if [ $((i % 2)) -eq 0 ]; then return 0; fi
      return 1
      ;;
    *)
      echo "Modo de error invalido: $SHOULD_ERROR" >&2
      exit 2
      ;;
  esac
}

run_login() {
  i="$1"
  if should_fail_iteration "$i"; then
    request POST /api/login "$(login_body "$BAD_USERNAME" "$BAD_PASSWORD")" >/dev/null
  else
    if [ -z "$USERNAME" ] || [ -z "$PASSWORD" ]; then
      echo "Login success requiere --username/--password o PHARMAGO_USERNAME/PHARMAGO_PASSWORD" >&2
      request POST /api/login "$(login_body "$BAD_USERNAME" "$BAD_PASSWORD")" >/dev/null
    else
      request POST /api/login "$(login_body "$USERNAME" "$PASSWORD")" >/dev/null
    fi
  fi
}

run_purchase_create() {
  i="$1"
  if should_fail_iteration "$i"; then
    request POST /api/purchases "$(purchase_body_error)" >/dev/null
  else
    if ! ensure_purchase_item; then
      return
    fi
    request POST /api/purchases "$(purchase_body_success)" >/dev/null
  fi
}

run_purchase_status_one() {
  i="$1"
  action="$2"
  if should_fail_iteration "$i"; then
    if ! ensure_token; then
      echo "No hay token valido para generar status failed dentro del controller. Se enviara igual para mover error rate HTTP." >&2
    fi
    target_purchase_id="$PURCHASE_ID"
    [ -z "$target_purchase_id" ] && target_purchase_id=999999
    request PUT "/api/purchases/$action/$target_purchase_id" "$(status_body_error)" "$TOKEN" >/dev/null
  else
    if ! ensure_token; then
      echo "No hay token valido para $action. Usa --token o --username/--password." >&2
      request PUT "/api/purchases/$action/999999" "$(status_body_error)" "$TOKEN" >/dev/null
    else
      target_purchase_id="$PURCHASE_ID"
      if [ -z "$target_purchase_id" ]; then
        if ! create_valid_purchase; then
          return
        fi
        target_purchase_id="$LAST_PURCHASE_ID"
      else
        ensure_purchase_item || return
      fi
      request PUT "/api/purchases/$action/$target_purchase_id" "$(status_body_success)" "$TOKEN" >/dev/null
    fi
  fi
}

run_one() {
  i="$1"
  case "$FUNCTIONALITY" in
    login) run_login "$i" ;;
    purchase-create) run_purchase_create "$i" ;;
    purchase-approve) run_purchase_status_one "$i" Approve ;;
    purchase-reject) run_purchase_status_one "$i" Reject ;;
    purchase-status)
      if [ $((i % 2)) -eq 0 ]; then
        run_purchase_status_one "$i" Approve
      else
        run_purchase_status_one "$i" Reject
      fi
      ;;
    all)
      case $((i % 4)) in
        0) run_login "$i" ;;
        1) run_purchase_create "$i" ;;
        2) run_purchase_status_one "$i" Approve ;;
        3) run_purchase_status_one "$i" Reject ;;
      esac
      ;;
    *)
      echo "Funcionalidad invalida: $FUNCTIONALITY" >&2
      exit 2
      ;;
  esac
}

menu() {
  echo "PharmaGo Business Metrics Chaos"
  echo "1) Login"
  echo "2) Crear compras"
  echo "3) Aprobar compra"
  echo "4) Rechazar compra"
  echo "5) Cambios de estado approve/reject"
  echo "6) Todas"
  printf 'Seleccione funcionalidad [1-6]: '
  read choice
  case "$choice" in
    1) FUNCTIONALITY=login ;;
    2) FUNCTIONALITY=purchase-create ;;
    3) FUNCTIONALITY=purchase-approve ;;
    4) FUNCTIONALITY=purchase-reject ;;
    5) FUNCTIONALITY=purchase-status ;;
    6) FUNCTIONALITY=all ;;
    *) echo "Opcion invalida" >&2; exit 2 ;;
  esac

  printf 'Debe dar error? yes/no/mixed [%s]: ' "$SHOULD_ERROR"
  read input
  [ -n "$input" ] && SHOULD_ERROR="$input"

  printf 'Cantidad de requests [%s]: ' "$COUNT"
  read input
  [ -n "$input" ] && COUNT="$input"

  printf 'Demora entre requests en ms [%s]: ' "$DELAY_MS"
  read input
  [ -n "$input" ] && DELAY_MS="$input"

  if [ "$FUNCTIONALITY" = "login" ] || [ "$FUNCTIONALITY" = "all" ]; then
    printf 'Usuario para login exitoso opcional [%s]: ' "$USERNAME"
    read input
    [ -n "$input" ] && USERNAME="$input"
    printf 'Password para login exitoso opcional: '
    stty -echo 2>/dev/null || true
    read input
    stty echo 2>/dev/null || true
    printf '\n'
    [ -n "$input" ] && PASSWORD="$input"
  fi

  case "$FUNCTIONALITY" in
    purchase-approve|purchase-reject|purchase-status|all)
      printf 'Token existente opcional [%s]: ' "$TOKEN"
      read input
      [ -n "$input" ] && TOKEN="$input"
      ;;
  esac

  current_log="no"
  [ "$VERBOSE" = "1" ] && current_log="yes"
  printf "Imprimir log de cada request en terminal? yes/no [%s]: " "$current_log"
  read input
  case "$input" in
    yes|y|Y|s|S|si|SI) VERBOSE=1 ;;
    no|n|N) VERBOSE=0 ;;
    "") ;;
    *) echo "Opcion invalida para log por request: $input" >&2; exit 2 ;;
  esac
}

while [ $# -gt 0 ]; do
  case "$1" in
    -u|--url) BASE_URL="$2"; shift 2 ;;
    -f|--functionality) FUNCTIONALITY="$2"; shift 2 ;;
    -e|--error) SHOULD_ERROR="$2"; shift 2 ;;
    -n|--count) COUNT="$2"; shift 2 ;;
    -d|--delay-ms) DELAY_MS="$2"; shift 2 ;;
    --username) USERNAME="$2"; shift 2 ;;
    --password) PASSWORD="$2"; shift 2 ;;
    --bad-username) BAD_USERNAME="$2"; shift 2 ;;
    --bad-password) BAD_PASSWORD="$2"; shift 2 ;;
    --token) TOKEN="$2"; shift 2 ;;
    --purchase-id) PURCHASE_ID="$2"; shift 2 ;;
    --pharmacy-id) PHARMACY_ID="$2"; shift 2 ;;
    --drug-code) DRUG_CODE="$2"; shift 2 ;;
    --purchase-email) PURCHASE_EMAIL="$2"; shift 2 ;;
    -v|--verbose) VERBOSE=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Argumento desconocido: $1" >&2; usage; exit 2 ;;
  esac
done

if [ "$FUNCTIONALITY" = "menu" ]; then
  menu
fi

case "$SHOULD_ERROR" in yes|no|mixed) ;; *) echo "--error debe ser yes, no o mixed" >&2; exit 2 ;; esac
case "$COUNT" in ''|*[!0-9]*) echo "--count debe ser entero" >&2; exit 2 ;; esac
case "$DELAY_MS" in ''|*[!0-9]*) echo "--delay-ms debe ser entero" >&2; exit 2 ;; esac

printf 'Chaos business metrics: functionality=%s error=%s count=%s delay_ms=%s base_url=%s\n' "$FUNCTIONALITY" "$SHOULD_ERROR" "$COUNT" "$DELAY_MS" "$BASE_URL"

success=0
failed=0
i=1
while [ "$i" -le "$COUNT" ]; do
  before_token="$TOKEN"
  run_one "$i"
  [ -n "$TOKEN" ] && [ "$before_token" != "$TOKEN" ] && echo "Token obtenido por login."
  # El objetivo es mover metricas; no se corta ante errores HTTP esperados.
  success=$((success + 1))
  i=$((i + 1))
  sleep_delay
done

echo "Done. Requests enviados: $success. Revisa PharmaGo - Business y Golden Signals."

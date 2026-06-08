#!/usr/bin/env bash

# Menu general para pruebas operativas desde Git Bash.
# Uso: bash Codigo/menu.sh

set -u

BASE_URL="${BASE_URL:-http://127.0.0.1:5000}"
PUBLIC_ENDPOINT="${PUBLIC_ENDPOINT:-/api/drug}"
AUTH_ENDPOINT="${AUTH_ENDPOINT:-/api/login}"
LOGIN_USERNAME="${LOGIN_USERNAME:-admin}"
LOGIN_PASSWORD="${LOGIN_PASSWORD:-Abcdef12.}"
TOTAL_REQUESTS="${TOTAL_REQUESTS:-150}"
DELAY_SECONDS="${DELAY_SECONDS:-0.05}"

pause() {
    echo
    read -r -p "Presione Enter para continuar..." || true
}

safe_clear() {
    if command -v clear >/dev/null 2>&1; then
        clear
    fi
}

read_with_default() {
    local label="$1"
    local default_value="$2"
    local value

    read -r -p "$label [$default_value]: " value || value=""

    if [ -z "$value" ]; then
        printf '%s' "$default_value"
    else
        printf '%s' "$value"
    fi
}

configure_rate_limit_test() {
    echo
    echo "Configuracion de la prueba"
    BASE_URL="$(read_with_default "Base URL del API Gateway" "$BASE_URL")"
    PUBLIC_ENDPOINT="$(read_with_default "Endpoint simple sin login" "$PUBLIC_ENDPOINT")"
    TOTAL_REQUESTS="$(read_with_default "Cantidad maxima de requests" "$TOTAL_REQUESTS")"
    DELAY_SECONDS="$(read_with_default "Pausa entre requests en segundos" "$DELAY_SECONDS")"
}

configure_login() {
    echo
    echo "Configuracion de login"
    AUTH_ENDPOINT="$(read_with_default "Endpoint de login" "$AUTH_ENDPOINT")"
    LOGIN_USERNAME="$(read_with_default "Usuario" "$LOGIN_USERNAME")"
    LOGIN_PASSWORD="$(read_with_default "Password" "$LOGIN_PASSWORD")"
}

extract_json_value() {
    local json="$1"
    local key="$2"

    printf '%s' "$json" \
        | sed -n "s/.*\"$key\"[[:space:]]*:[[:space:]]*\"\\([^\"]*\\)\".*/\\1/p" \
        | head -n 1
}

login_and_get_token() {
    local login_url="${BASE_URL}${AUTH_ENDPOINT}"
    local payload
    local response
    local http_code
    local body
    local token

    payload="{\"userName\":\"${LOGIN_USERNAME}\",\"password\":\"${LOGIN_PASSWORD}\"}"

    echo >&2
    echo "Login en $login_url con usuario '$LOGIN_USERNAME'..." >&2

    response="$(curl -s -w $'\n%{http_code}' \
        -H "Content-Type: application/json" \
        -X POST \
        -d "$payload" \
        "$login_url")"

    http_code="$(printf '%s\n' "$response" | tail -n 1)"
    body="$(printf '%s\n' "$response" | sed '$d')"

    if [ "$http_code" != "200" ]; then
        echo "No se pudo iniciar sesion. HTTP $http_code" >&2
        echo "Respuesta: $body" >&2
        return 1
    fi

    token="$(extract_json_value "$body" "token")"

    if [ -z "$token" ]; then
        echo "El login respondio 200, pero no se pudo extraer el token." >&2
        echo "Respuesta: $body" >&2
        return 1
    fi

    printf '%s' "$token"
}

status_label() {
    local code="$1"

    case "$code" in
        200) printf 'OK' ;;
        401) printf 'UNAUTHORIZED' ;;
        403) printf 'FORBIDDEN' ;;
        404) printf 'NOT_FOUND' ;;
        429) printf 'RATE_LIMITED' ;;
        000) printf 'NO_RESPONSE' ;;
        *) printf 'HTTP_%s' "$code" ;;
    esac
}

run_rate_limit_test() {
    local title="$1"
    local endpoint="$2"
    shift 2
    local curl_headers=("$@")
    local url="${BASE_URL}${endpoint}"
    local success=0
    local rate_limited=0
    local errors=0
    local current_minute
    local minute_bucket
    local minute_count=0
    local started_at
    local elapsed
    local rpm
    local i
    local code
    local label

    echo
    echo "$title"
    echo "URL: $url"
    echo "Max requests: $TOTAL_REQUESTS"
    echo "Delay: ${DELAY_SECONDS}s"
    echo
    echo "Enviando requests. Se detiene al primer 429 para ver exactamente cuando corta."
    echo

    started_at="$(date +%s)"
    minute_bucket="$(date +%Y%m%d%H%M)"

    for i in $(seq 1 "$TOTAL_REQUESTS"); do
        current_minute="$(date +%Y%m%d%H%M)"
        if [ "$current_minute" != "$minute_bucket" ]; then
            minute_bucket="$current_minute"
            minute_count=0
            echo "--- Nuevo minuto: contador por minuto reiniciado ---"
        fi

        minute_count=$((minute_count + 1))

        code="$(curl -s -o /dev/null -w "%{http_code}" "${curl_headers[@]}" "$url")"
        label="$(status_label "$code")"

        elapsed=$(( $(date +%s) - started_at ))
        if [ "$elapsed" -le 0 ]; then
            rpm="$minute_count"
        else
            rpm=$(( i * 60 / elapsed ))
        fi

        if [ "$code" = "200" ]; then
            success=$((success + 1))
        elif [ "$code" = "429" ]; then
            rate_limited=$((rate_limited + 1))
        else
            errors=$((errors + 1))
        fi

        printf '[%3d/%s] minuto=%3d rpm_aprox=%4s status=%s (%s)\n' \
            "$i" "$TOTAL_REQUESTS" "$minute_count" "$rpm" "$code" "$label"

        if [ "$code" = "429" ]; then
            echo
            echo "Rate limit activado en el request $i, con $minute_count requests en el minuto actual."
            break
        fi

        sleep "$DELAY_SECONDS"
    done

    echo
    echo "Resultados"
    echo "  Exitosas 200: $success"
    echo "  Rate limited 429: $rate_limited"
    echo "  Otros estados/errores: $errors"

    if [ "$rate_limited" -gt 0 ]; then
        echo "  Resultado: el rate limiting corto correctamente."
    else
        echo "  Resultado: no se observo 429 dentro de $TOTAL_REQUESTS requests."
    fi
}

basic_rate_limit_test() {
    configure_rate_limit_test
    run_rate_limit_test "Prueba basica sin login (rate limit por IP/fallback)" "$PUBLIC_ENDPOINT"
    pause
}

logged_user_rate_limit_test() {
    local token

    configure_rate_limit_test
    configure_login

    token="$(login_and_get_token)" || {
        pause
        return
    }

    run_rate_limit_test \
        "Prueba con usuario logueado (rate limit por usuario/token)" \
        "$PUBLIC_ENDPOINT" \
        -H "Authorization: Bearer $token" \
        -H "X-User-Id: $LOGIN_USERNAME"

    pause
}

rate_limit_menu() {
    local option

    while true; do
        safe_clear
        echo "Test rate limit"
        echo "==============="
        echo "1 - Prueba basica sobre endpoint simple sin login"
        echo "2 - Prueba con usuario logueado"
        echo "0 - Volver"
        echo

        if ! read -r -p "Seleccione una opcion: " option; then
            echo
            echo "Entrada cerrada. Volviendo..."
            return
        fi

        case "$option" in
            1) basic_rate_limit_test ;;
            2) logged_user_rate_limit_test ;;
            0) return ;;
            *) echo "Opcion invalida"; pause ;;
        esac
    done
}

main_menu() {
    local option

    while true; do
        safe_clear
        echo "Menu principal"
        echo "=============="
        echo "1 - Test rate limit"
        echo "0 - Salir"
        echo

        if ! read -r -p "Seleccione una opcion: " option; then
            echo
            echo "Entrada cerrada. Saliendo..."
            exit 0
        fi

        case "$option" in
            1) rate_limit_menu ;;
            0) echo "Saliendo..."; exit 0 ;;
            *) echo "Opcion invalida"; pause ;;
        esac
    done
}

main_menu

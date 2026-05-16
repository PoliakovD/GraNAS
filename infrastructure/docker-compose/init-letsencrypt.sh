#!/bin/bash
# Получение/проверка TLS-сертификата Let's Encrypt.
# Запускать перед стартом сервисов.
# Требования: порт 80 свободен, DNS домена уже указывает на этот VDS.
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

DOMAIN=$(grep -m1 '^DOMAIN=' vds.env | cut -d= -f2- | tr -d '"' | tr -d "'")
CERTBOT_EMAIL=$(grep -m1 '^CERTBOT_EMAIL=' vds.env | cut -d= -f2- | tr -d '"' | tr -d "'")

: "${DOMAIN:?Установи DOMAIN в vds.env}"
: "${CERTBOT_EMAIL:?Установи CERTBOT_EMAIL в vds.env}"

COMPOSE="docker compose --env-file vds.env -f compose.yaml -f compose.vds.yaml"

# ── 0. Проверяем: есть ли уже действующий сертификат от Let's Encrypt ──
echo "==> Проверка существующего сертификата для ${DOMAIN}..."
CERT_STATUS=$(
  $COMPOSE run --rm --entrypoint sh certbot -c "
    CERT=/etc/letsencrypt/live/${DOMAIN}/fullchain.pem
    if [ ! -f \"\$CERT\" ]; then echo invalid; exit 0; fi
    openssl x509 -in \"\$CERT\" -noout -checkend 604800 2>/dev/null || { echo invalid; exit 0; }
    openssl x509 -in \"\$CERT\" -noout -issuer 2>/dev/null | grep -qi 'let' || { echo invalid; exit 0; }
    echo valid
  " 2>/dev/null | tail -1
) || CERT_STATUS="invalid"

if [ "$CERT_STATUS" = "valid" ]; then
  echo "   Сертификат Let's Encrypt действующий (>7 дней), certbot пропускается."
  exit 0
fi

echo "   Действующего сертификата нет — получаем новый."

# ── 1. Создаём временный самоподписанный сертификат (нужен чтобы nginx стартовал) ──
echo "==> Создание временного сертификата для ${DOMAIN}..."
$COMPOSE run --rm --entrypoint sh certbot -c "
  set -e
  mkdir -p /etc/letsencrypt/live/${DOMAIN}
  if [ ! -f /etc/letsencrypt/live/${DOMAIN}/fullchain.pem ]; then
    openssl req -x509 -nodes -newkey rsa:2048 -days 1 \
      -keyout /etc/letsencrypt/live/${DOMAIN}/privkey.pem \
      -out    /etc/letsencrypt/live/${DOMAIN}/fullchain.pem \
      -subj '/CN=${DOMAIN}' 2>/dev/null
    echo 'Временный сертификат создан.'
  else
    echo 'Файл уже существует, пропускаем.'
  fi
"

# ── 2. Запускаем только nginx (для ACME challenge нужен только порт 80) ──
echo "==> Запуск nginx..."
$COMPOSE up -d --no-deps nginx

echo "   Ожидание готовности nginx на порту 80..."
NGINX_READY=0
for i in $(seq 1 20); do
  HTTP_CODE=$(curl -so /dev/null -w "%{http_code}" --connect-timeout 2 "http://localhost/" 2>/dev/null || echo "000")
  if echo "$HTTP_CODE" | grep -qE "^[234]"; then
    echo "   nginx готов (HTTP $HTTP_CODE)."
    NGINX_READY=1
    break
  fi
  echo "   Попытка $i/20 (HTTP $HTTP_CODE)..."
  sleep 3
done

if [ "$NGINX_READY" -eq 0 ]; then
  echo ""
  echo "==> ДИАГНОСТИКА: nginx не отвечает на порту 80 после 60 сек"
  echo "--- docker ps (nginx) ---"
  docker ps -a --filter "name=nginx" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" || true
  echo "--- nginx logs ---"
  $COMPOSE logs --tail=50 nginx 2>&1 || true
  echo "--- netstat port 80 ---"
  ss -tlnp | grep :80 || echo "(порт 80 не слушает)"
  exit 1
fi

# ── 3. Тянем свежий certbot и удаляем временный серт (чтобы certbot создал чистый) ──
echo "==> Обновление образа certbot..."
$COMPOSE pull certbot

echo "==> Удаление временного сертификата перед получением настоящего..."
$COMPOSE run --rm --entrypoint sh certbot -c "
  rm -rf /etc/letsencrypt/live/${DOMAIN}
  rm -rf /etc/letsencrypt/archive/${DOMAIN}
  rm -f /etc/letsencrypt/renewal/${DOMAIN}.conf
"

# ── 4. Получаем настоящий сертификат Let's Encrypt через webroot ──
echo "==> Получение сертификата Let's Encrypt для ${DOMAIN}..."
$COMPOSE run --rm --entrypoint certbot certbot certonly \
  --webroot -w /var/www/certbot \
  --email "${CERTBOT_EMAIL}" \
  --agree-tos --no-eff-email \
  -d "${DOMAIN}"

# ── 5. Регистрируем cron для ежемесячной перезагрузки nginx ──
CRON_CMD="0 4 1 * * docker compose --env-file ${SCRIPT_DIR}/vds.env -f ${SCRIPT_DIR}/compose.yaml -f ${SCRIPT_DIR}/compose.vds.yaml exec -T nginx nginx -s reload >> /var/log/nginx-reload.log 2>&1"
( crontab -l 2>/dev/null | grep -v "nginx -s reload" ; echo "$CRON_CMD" ) | crontab -
echo "==> Cron для перезагрузки nginx зарегистрирован."

echo "==> Сертификат получен успешно."

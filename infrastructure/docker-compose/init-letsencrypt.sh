#!/bin/bash
# Первичная настройка HTTPS (запускать один раз перед docker compose up).
# Требования: порт 80 свободен, DNS домена уже указывает на этот VDS.
#
# Использование: bash init-letsencrypt.sh
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

DOMAIN=$(grep -m1 '^DOMAIN=' vds.env | cut -d= -f2- | tr -d '"' | tr -d "'")
CERTBOT_EMAIL=$(grep -m1 '^CERTBOT_EMAIL=' vds.env | cut -d= -f2- | tr -d '"' | tr -d "'")

: "${DOMAIN:?Установи DOMAIN в vds.env}"
: "${CERTBOT_EMAIL:?Установи CERTBOT_EMAIL в vds.env}"

COMPOSE="docker compose --env-file vds.env -f compose.yaml -f compose.vds.yaml"

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
    echo 'Сертификат уже существует, пропускаем.'
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
  echo ""
  exit 1
fi

# ── 3. Получаем настоящий сертификат Let's Encrypt через webroot ──
echo "==> Получение сертификата Let's Encrypt для ${DOMAIN}..."
$COMPOSE run --rm --entrypoint certbot certbot certonly \
  --webroot -w /var/www/certbot \
  --email "${CERTBOT_EMAIL}" \
  --agree-tos --no-eff-email \
  --overwrite-existing-cert \
  --verbose \
  -d "${DOMAIN}"

# ── 4. Перезагружаем nginx с настоящим сертификатом ──
echo "==> Перезагрузка nginx..."
$COMPOSE exec nginx nginx -s reload

# ── 5. Регистрируем cron для ежемесячной перезагрузки nginx (подхватывает обновлённый сертификат) ──
CRON_CMD="0 4 1 * * docker compose --env-file ${SCRIPT_DIR}/vds.env -f ${SCRIPT_DIR}/compose.yaml -f ${SCRIPT_DIR}/compose.vds.yaml exec -T nginx nginx -s reload >> /var/log/nginx-reload.log 2>&1"
( crontab -l 2>/dev/null | grep -v "nginx -s reload" ; echo "$CRON_CMD" ) | crontab -
echo "==> Cron для перезагрузки nginx зарегистрирован (1-е число каждого месяца)."

echo ""
echo "╔══════════════════════════════════════════════════════════╗"
echo "║  HTTPS настроен! Запусти все сервисы командой:          ║"
echo "║                                                          ║"
echo "║  docker compose --env-file vds.env \\                    ║"
echo "║    -f compose.yaml -f compose.vds.yaml up -d            ║"
echo "╚══════════════════════════════════════════════════════════╝"

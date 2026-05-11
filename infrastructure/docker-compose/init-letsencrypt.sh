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

# ── 2. Запускаем nginx с временным сертификатом ──
echo "==> Запуск nginx (web-client + api-gateway)..."
$COMPOSE up -d nginx web-client api-gateway
echo "   Ожидание готовности nginx..."
sleep 6

# ── 3. Удаляем self-signed cert из volume: nginx его уже загрузил в память,
#       а certbot должен создать lineage с чистым именем без суффикса -0001 ──
$COMPOSE run --rm --entrypoint sh certbot -c "
  rm -rf /etc/letsencrypt/live/${DOMAIN}
  rm -rf /etc/letsencrypt/archive/${DOMAIN}
  rm -f  /etc/letsencrypt/renewal/${DOMAIN}.conf
"

# ── 4. Получаем настоящий сертификат Let's Encrypt через webroot ──
echo "==> Получение сертификата Let's Encrypt для ${DOMAIN}..."
$COMPOSE run --rm certbot certonly \
  --webroot -w /var/www/certbot \
  --email "${CERTBOT_EMAIL}" \
  --agree-tos --no-eff-email \
  -d "${DOMAIN}"

# ── 5. Перезагружаем nginx с настоящим сертификатом ──
echo "==> Перезагрузка nginx..."
$COMPOSE exec nginx nginx -s reload

# ── 6. Регистрируем cron для ежемесячной перезагрузки nginx (подхватывает обновлённый сертификат) ──
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

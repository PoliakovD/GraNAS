#!/bin/sh
# Reads DASHBOARDS_PASSWORD from vds.env and writes nginx/htpasswd.
# Run from the directory that contains vds.env (infrastructure/docker-compose/).
set -eu

cd "$(dirname "$0")"

if [ ! -f vds.env ]; then
  echo "Error: vds.env not found in $(pwd)" >&2
  exit 1
fi

DASHBOARDS_PASSWORD=$(grep '^DASHBOARDS_PASSWORD=' vds.env | cut -d= -f2-)

if [ -z "$DASHBOARDS_PASSWORD" ]; then
  echo "Error: DASHBOARDS_PASSWORD is not set in vds.env" >&2
  exit 1
fi

mkdir -p nginx
HASH=$(openssl passwd -apr1 -- "$DASHBOARDS_PASSWORD")
printf 'admin:%s\n' "$HASH" > nginx/htpasswd
echo "nginx/htpasswd generated"

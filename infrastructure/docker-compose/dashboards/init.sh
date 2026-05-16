#!/bin/sh
set -eu

URL="http://opensearch-dashboards:5601/_dashboards"

echo "[dashboards-init] Waiting for OpenSearch Dashboards to be ready..."
i=0
while [ $i -lt 60 ]; do
  if curl -fs "$URL/api/status" >/dev/null 2>&1; then
    echo "[dashboards-init] Dashboards is up."
    break
  fi
  i=$((i + 1))
  sleep 2
done

if [ $i -eq 60 ]; then
  echo "[dashboards-init] ERROR: Dashboards did not become ready in 120s."
  exit 1
fi

echo "[dashboards-init] Importing saved objects..."
curl -fsS -X POST \
  -H "osd-xsrf: true" \
  -F "file=@/work/granas-debug.ndjson" \
  "$URL/api/saved_objects/_import?overwrite=true"

echo ""
echo "[dashboards-init] Done."

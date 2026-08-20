#!/usr/bin/env bash
set -euo pipefail
# Smoke the published stack through the gateway (host port 80 by default).
BASE="${1:-http://127.0.0.1:${GATEWAY_PORT:-80}}"
echo "gateway health: $(curl -fsS "$BASE/health")"
echo "web health:     $(curl -fsS "$BASE/web/health")"
echo "api health:     $(curl -fsS "$BASE/api/health")"
echo "nfe health:     $(curl -fsS "$BASE/nfe/health")"
echo "worker health:  $(curl -fsS "$BASE/worker/health")"
PASS="${BOOTSTRAP_ADMIN_PASSWORD:-VilmoAdmin!2026}"
TOKEN=$(curl -fsS -X POST "$BASE/api/auth/login" -H 'content-type: application/json' \
  -d "{\"email\":\"admin@vilmomkt.com\",\"password\":\"$PASS\"}" | python3 -c 'import sys,json; print(json.load(sys.stdin)["accessToken"])')
echo "login: ok"
curl -fsS "$BASE/api/me" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool | head -40
echo "web login page:"
curl -fsS -o /dev/null -w "%{http_code} %{content_type}\n" "$BASE/web/login.html"

#!/usr/bin/env bash
set -euo pipefail

BASE="${GATEWAY_BASE:-http://127.0.0.1:${GATEWAY_PORT:-80}}"

expect_service() {
  local path="$1"
  local service="$2"
  local body
  body="$(curl -fsS "${BASE}${path}")"
  echo "${path} -> ${body}"
  echo "${body}" | grep -q "\"service\":\"${service}\""
}

echo "== gateway health =="
curl -fsS "${BASE}/health"
echo

expect_service "/web/" "vilmo-web"
expect_service "/api/" "vilmo-api"
expect_service "/nfe/" "vilmo-nfe"
expect_service "/worker/" "vilmo-worker"

echo "== marketplace root paths hit vilmo-api =="
body="$(curl -fsS "${BASE}/webhooks/MercadoLivre")"
echo "/webhooks/MercadoLivre -> ${body}"
echo "${body}" | grep -q '"service":"vilmo-api"'
echo "${body}" | grep -q '/webhooks/MercadoLivre'

body="$(curl -fsS "${BASE}/oauth/Shopee/callback")"
echo "/oauth/Shopee/callback -> ${body}"
echo "${body}" | grep -q '"service":"vilmo-api"'

echo "== slug prefix is stripped for /api =="
body="$(curl -fsS "${BASE}/api/auth/login")"
echo "/api/auth/login -> ${body}"
echo "${body}" | grep -q '"path":"/auth/login"'

echo "== backends must not be published on the host =="
if ss -lnt | awk '{print $4}' | grep -E '(:8080|:8081|:5432|:6379)$' >/dev/null; then
  echo "FAIL: found a backend port published on the host"
  ss -lnt | grep -E '(:8080|:8081|:5432|:6379)\b' || true
  exit 1
fi

echo "OK"

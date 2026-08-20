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

echo "US-17 PT home:"
pt_code=$(curl -sS -D /tmp/vilmo-pt.hdr -o /tmp/vilmo-pt.html -w "%{http_code}" "$BASE/")
echo "$pt_code"
test "$pt_code" = "200"
! grep -qi '^[Ll]ocation:' /tmp/vilmo-pt.hdr
grep -q "68.431.371/0001-61" /tmp/vilmo-pt.html
grep -q "admin@vilmomkt.com" /tmp/vilmo-pt.html
grep -q 'hreflang="en"' /tmp/vilmo-pt.html
grep -q 'id="cookie-bar"' /tmp/vilmo-pt.html
! grep -qi 'googletagmanager\|google-analytics\|gtag(' /tmp/vilmo-pt.html
echo "US-17 EN home:"
en_code=$(curl -sS -o /tmp/vilmo-en.html -w "%{http_code}" "$BASE/en/")
echo "$en_code"
test "$en_code" = "200"
grep -q "Sign in" /tmp/vilmo-en.html
grep -q "68.431.371/0001-61" /tmp/vilmo-en.html
for path in /privacidade/ /termos/ /cookies/ /contato/ /en/privacy/ /en/terms/ /en/cookies/ /en/contact/; do
  code=$(curl -sS -o /dev/null -w "%{http_code}" "$BASE$path")
  echo "$path $code"
  test "$code" = "200"
done
curl -fsS "$BASE/robots.txt" | grep -q "Sitemap:"
curl -fsS "$BASE/sitemap.xml" | grep -q "/en/"
echo "US-17: ok"

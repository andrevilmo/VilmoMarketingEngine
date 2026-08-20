#!/bin/bash
# Run on the EC2 host as root after DNS A records point at this instance.
set -euxo pipefail
cd /opt/vilmo

dnf install -y certbot || pip3 install certbot

docker stop vilmo-gateway || true
certbot certonly --standalone --non-interactive --agree-tos \
  --email admin@vilmomkt.com \
  -d vilmomkt.com -d www.vilmomkt.com
docker start vilmo-gateway || true

mkdir -p /opt/vilmo/aws
# nginx-ssl.conf must already exist at /opt/vilmo/aws/nginx-ssl.conf
test -f /opt/vilmo/aws/nginx-ssl.conf

cd /opt/vilmo
docker compose -f docker-compose.yml -f aws/docker-compose.aws.yml -f aws/docker-compose.tls.yml up -d --build vilmo-gateway

cat >/etc/cron.d/vilmo-certbot <<'CRON'
0 3 * * * root certbot renew --quiet --deploy-hook 'docker compose -f /opt/vilmo/docker-compose.yml -f /opt/vilmo/aws/docker-compose.aws.yml -f /opt/vilmo/aws/docker-compose.tls.yml exec -T vilmo-gateway nginx -s reload || docker restart vilmo-gateway'
CRON

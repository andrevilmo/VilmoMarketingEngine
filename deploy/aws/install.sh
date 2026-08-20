#!/bin/bash
# Runs on Amazon Linux 2023 after the deploy tarball is extracted to /opt/vilmo.
set -euxo pipefail

if [[ ! -f /opt/vilmo/docker-compose.yml ]]; then
  echo "missing /opt/vilmo/docker-compose.yml" >&2
  exit 1
fi

if ! swapon --show | grep -q /swapfile; then
  fallocate -l 2G /swapfile || dd if=/dev/zero of=/swapfile bs=1M count=2048
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  grep -q '/swapfile' /etc/fstab || echo '/swapfile swap swap defaults 0 0' >> /etc/fstab
fi

dnf install -y docker
systemctl enable --now docker

mkdir -p /usr/libexec/docker/cli-plugins
if [[ ! -x /usr/libexec/docker/cli-plugins/docker-compose ]]; then
  curl -fsSL -o /usr/libexec/docker/cli-plugins/docker-compose \
    https://github.com/docker/compose/releases/download/v2.29.7/docker-compose-linux-x86_64
  chmod +x /usr/libexec/docker/cli-plugins/docker-compose
fi

cd /opt/vilmo
docker compose -f docker-compose.yml -f aws/docker-compose.aws.yml up -d --build

cat >/etc/systemd/system/vilmo.service <<'UNIT'
[Unit]
Description=Vilmo Marketing Engine compose stack
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=/opt/vilmo
ExecStart=/usr/bin/docker compose -f docker-compose.yml -f aws/docker-compose.aws.yml up -d
ExecStop=/usr/bin/docker compose -f docker-compose.yml -f aws/docker-compose.aws.yml down
TimeoutStartSec=0

[Install]
WantedBy=multi-user.target
UNIT

systemctl enable vilmo.service

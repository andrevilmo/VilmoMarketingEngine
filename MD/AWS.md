# AWS (free plan) + vilmomkt.com

Never put AWS or app passwords in the repository. Do not use the AWS **root** user for daily work.

## Live

| | |
| --- | --- |
| Account | `099223714476` |
| Region | `sa-east-1` |
| Instance | `i-0cffbec1bda376260` (`t3.micro`) |
| Elastic IP | `54.94.59.184` |
| DNS | Squarespace A `@` and `www` → `54.94.59.184` |
| Mail | MX `smtp.google.com` unchanged |

**Public site (HTTPS):**

- https://vilmomkt.com/ — commercial index PT (US-17; today still 302 → `/web/` until that ships). Must show CNPJ **68.431.371/0001-61**, contact **`admin@vilmomkt.com`**, cookie bar, privacy/terms.
- https://vilmomkt.com/en/ — commercial index EN (same story, after US-17)
- https://vilmomkt.com/web/ — Metronic UI (login + app)
- https://vilmomkt.com/api/health — `vilmo-api`
- Seeded login: `admin@vilmomkt.com` (password from host env `BOOTSTRAP_ADMIN_PASSWORD`, not git)

HTTP on port 80 redirects to HTTPS. Gmail/Workspace is unchanged.

## Stack

Docker Compose on the box: nginx gateway, Metronic `vilmo-web`, `vilmo-api`, `vilmo-worker`, `vilmo-nfe`, Postgres, Redis.

TLS overlay: `deploy/aws/docker-compose.tls.yml` + `deploy/aws/nginx-ssl.conf`.

Publish from this repo:

```
# on the instance, as /opt/vilmo
docker compose -f docker-compose.yml -f aws/docker-compose.aws.yml -f aws/docker-compose.tls.yml up -d --build
```

A1 PFX lives only on the host: `/opt/vilmo/certs/68431371000161.pfx`.

# Docker production notes

Images are **built locally** from this directory (no container registry required). One image serves **all** Next template demos at:

`https://keenthemes.com/metronic/tailwind/nextjs/demo1/` … `/demo10/`

Routing uses middleware (`middleware.ts`) so one build does not require per-demo image tags.

## Layout on the server

Clone or sync the repo to:

`/opt/keenthemes/apps/metronic-tailwind-react`

From that directory:

```bash
mkdir -p typescript/nextjs/logs
docker compose -f .github/docker-compose.nextjs.yml --env-file typescript/nextjs/.env.production up -d --build
```

Compose bind-mounts logs to:

`${METRONIC_REACT_ROOT:-/opt/keenthemes/apps/metronic-tailwind-react}/typescript/nextjs/logs`

Set `METRONIC_REACT_ROOT` if the repo root is not `/opt/keenthemes/apps/metronic-tailwind-react`.

## Build arguments

| Argument | Typical value |
|----------|----------------|
| `NEXT_PUBLIC_BASE_PATH` | `/metronic/tailwind/nextjs` (path only). You may use a full `https://…` URL for `assetPrefix`; `next.config.mjs` accepts either. |

Runtime secrets (`DATABASE_URL`, `NEXTAUTH_*`, etc.) belong in `typescript/nextjs/.env.production` on the server, not in the image.

Use a **single** public base for all demos (no `/demo1` in the path), for example:

- `NEXT_PUBLIC_BASE_PATH=https://keenthemes.com/metronic/tailwind/nextjs/`
- `NEXTAUTH_URL=https://keenthemes.com/metronic/tailwind/nextjs/`

Do not use per-demo URLs here; demos are selected by the `/demo1` … `/demo10` URL segment and middleware.

## Rollback

Rebuild and redeploy from a known Git revision:

```bash
cd /opt/keenthemes/apps/metronic-tailwind-react
git checkout <sha>
docker compose -f .github/docker-compose.nextjs.yml --env-file typescript/nextjs/.env.production build --no-cache
docker compose -f .github/docker-compose.nextjs.yml --env-file typescript/nextjs/.env.production up -d
```

## Health check

Inside the container, Node listens with `basePath` `/metronic/tailwind/nextjs`:

`GET http://127.0.0.1:3000/metronic/tailwind/nextjs/api/health`

See `.github/docker-compose.nextjs.yml` for Traefik labels.

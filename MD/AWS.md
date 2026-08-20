# AWS (free plan) + vilmomkt.com later

Never put AWS or app passwords in the repository. Do not use the AWS **root** user for daily work — create an IAM user.

## Live (sa-east-1)

Stack `vilmo-gateway` is **CREATE_COMPLETE**. HTTP apps are still echo stubs.

| | |
| --- | --- |
| Account | `099223714476` |
| Instance | `i-0cffbec1b4a5782c0` (`t3.micro`) |
| Elastic IP | `54.94.59.184` |
| Health | http://54.94.59.184/health |
| Web slug | http://54.94.59.184/web/ |
| API slug | http://54.94.59.184/api/ |
| NF-e slug | http://54.94.59.184/nfe/ |
| Worker slug | http://54.94.59.184/worker/ |

SSM (no SSH): `aws ssm start-session --target i-0cffbec1b4a5782c0 --region sa-east-1`

## What is running

One **t3.micro** in **sa-east-1** (São Paulo), Amazon Linux 2023:

| On the box | On the internet |
| --- | --- |
| Docker Compose: gateway + `/web` `/api` `/nfe` `/worker` stubs, Postgres, Redis | **TCP 80** (and 443 reserved) |
| 2 GiB swap (1 GiB RAM) | Elastic IP `54.94.59.184` |
| SSM Session Manager (no SSH port) | Budget alert to `admin@vilmomkt.com` at $5/month |

No ALB, no RDS, no ElastiCache, no NAT gateway.

**Free plan (accounts from 15 Jul 2025):** up to **$200** credits over ~6 months, then the instance bills. Check Billing → Free Tier.

Template: `deploy/aws/cloudformation.yml` (regenerate with `python3 deploy/aws/generate-cfn.py` if gateway files change).

## Domain vilmomkt.com (later)

Today:

- Site is a Squarespace “Em breve” page.
- NS: Squarespace DNS.
- **MX already points at Google** (`smtp.google.com`) — Workspace/Gmail is already on this domain.

Cutover:

1. In Squarespace DNS, set **A** `@` and **www** to `54.94.59.184`.
2. **Do not change MX** if Gmail/Workspace should keep working.
3. Keep TXT SPF/DKIM/DMARC from Google.
4. After DNS propagates, add HTTPS. Marketplace OAuth/webhook URLs become `https://vilmomkt.com/oauth/...` and `https://vilmomkt.com/webhooks/...`.

Gmail / Google Workspace stays on Google; only the website A records move to AWS.

## App login (later, when the API exists)

Platform super user is `admin@vilmomkt.com`. Store the password in **SSM Parameter Store**, not in git. Rotate the AWS root password so it is not the same as the app or A1 certificate.

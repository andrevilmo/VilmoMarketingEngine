# AWS (free plan) + vilmomkt.com later

Live launch from this agent **stopped at AWS MFA**. Root email `admin@vilmomkt.com` exists and accepted the console password; AWS then asked for the MFA device code. No instance was created. Do not send MFA codes in git. After you sign in, launch the stack below.

Never put AWS or app passwords in the repository. Do not use the AWS **root** user for daily work — create an IAM user after MFA.

## What we will run (cheap)

One **t3.micro** (free-plan eligible) in **sa-east-1** (São Paulo), Amazon Linux 2023:

| On the box | On the internet |
| --- | --- |
| Docker Compose: gateway + `/web` `/api` `/nfe` `/worker` stubs, Postgres, Redis | **TCP 80** (and 443 reserved) |
| 2 GiB swap (1 GiB RAM) | Elastic IP |
| SSM Session Manager (no SSH port) | Budget alert to `admin@vilmomkt.com` at $5/month |

No ALB, no RDS, no ElastiCache, no NAT gateway.

**Free plan (accounts from 15 Jul 2025):** up to **$200** credits over ~6 months, then the instance bills. **Legacy** accounts (before that date) still get 750 h/month of t3.micro for 12 months. Check Billing → Free Tier.

Current HTTP apps are **echo stubs**. The .NET API is not built yet; this hosts the gateway topology.

## Launch after MFA

Region **sa-east-1**:

1. Console → CloudFormation → Create stack → Upload `deploy/aws/cloudformation.yml`.
2. Stack name `vilmo-gateway`. Instance type `t3.micro`.
3. Acknowledge IAM capabilities.
4. Wait until CREATE_COMPLETE (~5–8 min). Copy **PublicIp** / **GatewayUrl**.
5. Open `http://<PublicIp>/health` → `vilmo-gateway`. Then `/web/`, `/api/`, `/nfe/`, `/worker/`.

Regenerate the template if gateway files change:

```
python3 deploy/aws/generate-cfn.py
```

Shell on the instance (no SSH):

```
aws ssm start-session --target <instance-id> --region sa-east-1
```

## Domain vilmomkt.com (later)

Today:

- Site is a Squarespace “Em breve” page.
- NS: Squarespace DNS.
- **MX already points at Google** (`smtp.google.com`) — Workspace/Gmail is already on this domain.

Cutover when the Elastic IP is up:

1. In Squarespace DNS (or after moving NS to Route 53), set **A** `@` and **www** to the Elastic IP.
2. **Do not change MX** if Gmail/Workspace should keep working.
3. Keep TXT SPF/DKIM/DMARC from Google.
4. After DNS propagates, add HTTPS (Certbot or ACM + nginx). Marketplace OAuth/webhook URLs become `https://vilmomkt.com/oauth/...` and `https://vilmomkt.com/webhooks/...`.

Gmail / Google Workspace is **not** hosted on EC2. It stays on Google; only the website A records move to AWS.

## App login (later, when the API exists)

Platform super user is `admin@vilmomkt.com`. Store the password in **SSM Parameter Store**, not in git or UserData. Rotate the AWS root password so it is not the same as the app or A1 certificate.

## This agent could not finish

Blocked on **Additional verification required** (MFA) at AWS sign-in. Send a follow-up after you are in the console (or paste a one-time MFA code in chat **only if you want this agent to click Sign in**), and we can click Create stack.

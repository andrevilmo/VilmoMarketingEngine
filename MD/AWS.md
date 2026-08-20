# AWS (free plan) + vilmomkt.com

Never put AWS or app passwords in the repository. Do not use the AWS **root** user for daily work — create an IAM user.

## Live

| | |
| --- | --- |
| Account | `099223714476` |
| Region | `sa-east-1` |
| Instance | `i-0cffbec1b4a5782c0` (`t3.micro`) |
| Elastic IP | `54.94.59.184` |
| DNS | Squarespace (Google login) A `@` and `www` → `54.94.59.184` |
| Mail | MX `smtp.google.com` unchanged |

**Site (HTTPS, Let’s Encrypt):**

- https://vilmomkt.com/health → `vilmo-gateway`
- https://vilmomkt.com/web/
- https://www.vilmomkt.com/web/
- https://vilmomkt.com/api/

HTTP on port 80 redirects to HTTPS. Gmail/Workspace is unchanged (MX + SPF + DKIM kept).

SSM: `aws ssm start-session --target i-0cffbec1b4a5782c0 --region sa-east-1`

## Stack

Docker Compose on the box: gateway slugs, Postgres, Redis. No ALB, RDS, ElastiCache, or NAT.

Template: `deploy/aws/cloudformation.yml`. TLS overlay: `deploy/aws/docker-compose.tls.yml` + `deploy/aws/enable-https.sh`.

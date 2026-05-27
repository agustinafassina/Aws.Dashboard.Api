# Aws.Api (.NET 10) ☁️
Security-focused Web API for AWS. It connects to your AWS account and surfaces resources that may not follow cloud security best practices.

**Status:** early stage — S3, EC2, RDS, IAM access keys, cost-by-project and Inspector vulnerabilities are available. More checks are planned.

## Vision 🎯
Help teams audit and improve their AWS posture by exposing misconfigurations, risky permissions, stale credentials, and other gaps against well-known best practices.

### Roadmap 🗺️
| Area | Planned checks |
|------|----------------|
| **S3** | Public buckets *(available)* |
| **EC2** | Instances, security groups and open inbound ports *(available)* |
| **RDS** | Database instances, security groups and open inbound ports *(available)* |
| **IAM** | Access keys listing and rotation checks *(available)* |
| **IAM** | Users without MFA, roles and permissions aligned with least privilege |
| **Containers** | Amazon Inspector vulnerabilities for EC2, Lambda and ECR *(available)* |
| **Cost** | Costs grouped by project tag with date range *(available)* |
| **More** | Additional security and governance checks over time |

## Requirements ✅
- .NET 10 SDK
- AWS credentials with read permissions for the resources being scanned
- (Optional) Docker

## Solution structure 📦
| Project | Responsibility |
|---------|----------------|
| **Aws.Api** | HTTP entry point, controllers, middleware, Swagger |
| **Aws.Services** | Application logic and orchestration |
| **Aws.Repository** | AWS SDK integration and data access |
| **Aws.Models** | DTOs, shared models, and configuration classes |

## Run locally ▶️
```bash
dotnet build
dotnet run --project Aws.Api
```

- Swagger: `http://localhost:5142/swagger`
- Health check: `GET /health`

## Docker 🐳
```bash
docker build -f Dockerfile -t aws-api:latest .
docker run -d -p 8787:80 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e AWS_ACCESS_KEY_ID=your-key \
  -e AWS_SECRET_ACCESS_KEY=your-secret \
  -e AWS__Region=us-east-1 \
  --name aws-api aws-api:latest
```

Swagger: `http://localhost:8787/swagger`

## Configuration ⚙️
### AWS credentials 🔑
Uses the [AWS SDK default credential chain](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/creds-locate.html): env vars, `~/.aws/credentials`, or IAM role.

Default region in `appsettings.json` — override with `AWS__Region=us-east-1`.

### Service options
| Section | Purpose | Env var prefix |
|---------|---------|----------------|
| `S3` | Public ACL grantee URIs | `S3__` |
| `Ec2` | CIDRs treated as public exposure (`0.0.0.0/0`, `::/0`) | `Ec2__PublicInboundCidrs__` |
| `Rds` | CIDRs treated as public exposure | `Rds__PublicInboundCidrs__` |
| `Iam` | Max age (days) before access keys need rotation | `Iam__AccessKeyRotationMaxAgeDays` |
| `Cost` | Tag key used to group costs by project, and cost metric | `Cost__ProjectTagKey`, `Cost__Metric` |
| `Inspector` | Page size and max findings cap for vulnerability scans | `Inspector__PageSize`, `Inspector__MaxTotalFindings` |

See `appsettings.json` for default values.

## API endpoints 🛡️
Regional endpoints require `?region=` (e.g. `us-east-1`). IAM and Cost Explorer are global. Full schemas are in **Swagger**.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/s3/public-buckets` | S3 buckets publicly accessible (policy, ACL, Block Public Access) |
| `GET` | `/api/v1/ec2/open-ports` | EC2 instances, security groups and inbound ports open to the internet |
| `GET` | `/api/v1/rds/open-ports` | RDS instances, security groups, `PubliclyAccessible` flag and open ports |
| `GET` | `/api/v1/iam/access-keys` | IAM access keys for all users, age, last usage and rotation status |
| `GET` | `/api/v1/cost/by-project?startDate=&endDate=` | AWS costs grouped by project tag for an inclusive date range (`yyyy-MM-dd`) |
| `GET` | `/api/v1/inspector/vulnerabilities?region=&resourceType=&severity=&status=` | Amazon Inspector findings for EC2, Lambda or ECR (`resourceType`: `ec2`, `lambda`, `ecr`) |

## Contributing 🤝
1. Fork the repository
2. Create a branch (`feature/your-feature`)
3. Open a Pull Request describing your changes

## License 📜
Created by Agustina Fassina.

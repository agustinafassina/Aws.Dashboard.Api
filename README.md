# Aws.Api (.NET 10) ☁️
Security-focused Web API for AWS. It connects to your AWS account and surfaces resources that may not follow cloud security best practices.

**Status:** active development — security checks across S3, EC2, RDS, IAM, Lambda, ELB, ECR, ACM, Inspector, audits, cost and a regional security summary dashboard.

## Vision 🎯
Help teams audit and improve their AWS posture by exposing misconfigurations, risky permissions, stale credentials, and other gaps against well-known best practices.

### Coverage 🗺️
| Area | Checks |
|------|--------|
| **Summary** | Regional scorecard aggregating counters from all scans |
| **S3** | Public buckets, encryption status |
| **EC2** | Open ports, unused security groups, unattached volumes, IMDSv1 |
| **RDS** | Open ports, unencrypted instances, backup retention |
| **IAM** | Access keys, MFA, risky/overprivileged/inline policies, admin grants, cross-account roles, root account |
| **Lambda** | Publicly invocable functions |
| **ELB** | Internet-facing load balancers and unencrypted HTTP listeners |
| **ECR** | Public repository policies, scan-on-push disabled |
| **ACM** | Expiring and expired certificates |
| **Inspector** | Vulnerabilities for EC2, Lambda and ECR |
| **Audits** | Untagged resources and resources by project tag |
| **Cost** | Costs grouped by project tag |

## Requirements ✅
- .NET 10 SDK.
- AWS credentials with read permissions for the resources being scanned.
- (Optional) Docker.

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
- Example requests: `AwsApi.http`

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
| `Rds` | Public exposure CIDRs and minimum backup retention days | `Rds__PublicInboundCidrs__`, `Rds__MinBackupRetentionDays` |
| `Iam` | Max age (days) before access keys need rotation | `Iam__AccessKeyRotationMaxAgeDays` |
| `Cost` | Tag key used to group costs by project, and cost metric | `Cost__ProjectTagKey`, `Cost__Metric` |
| `Inspector` | Page size and max findings cap for vulnerability scans | `Inspector__PageSize`, `Inspector__MaxTotalFindings` |
| `Cors` | Allowed browser origins for the API | `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, … |

See `appsettings.json` for default values.

### CORS 🌐
Origins are read from `Cors:AllowedOrigins` in configuration. In **Development**, `http://localhost:3000` and `http://127.0.0.1:3000` are added automatically if not already listed. If no origins are configured, the API falls back to `http://localhost:3000`.

Example for a frontend on another port:
```json
"Cors": {
  "AllowedOrigins": [ "http://localhost:5173", "https://dashboard.example.com" ]
}
```

## API endpoints 🛡️
Regional endpoints require `?region=` (e.g. `us-east-1`). IAM and Cost Explorer are global. Full schemas are in **Swagger**.

### Security summary
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/security/summary?region=&days=30` | Scorecard with finding counters from all scans (ideal for dashboards) |

### S3
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/s3/public-buckets?region=` | Publicly accessible buckets |
| `GET` | `/api/v1/s3/encryption-status?region=` | Buckets without default encryption |

### EC2
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/ec2/open-ports?region=` | Instances with inbound ports open to the internet |
| `GET` | `/api/v1/ec2/unused-security-groups?region=` | Security groups not attached to any ENI |
| `GET` | `/api/v1/ec2/unattached-volumes?region=` | EBS volumes with no attachment |
| `GET` | `/api/v1/ec2/imdsv1-instances?region=` | Instances allowing IMDSv1 |

### RDS
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/rds/open-ports?region=` | Instances with public exposure or open security group rules |
| `GET` | `/api/v1/rds/unencrypted-instances?region=` | Instances without encryption at rest |
| `GET` | `/api/v1/rds/no-backups?region=` | Instances with backups disabled or retention below minimum |

### IAM (global)
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/iam/access-keys` | Access keys, age, last usage and rotation status |
| `GET` | `/api/v1/iam/users-without-mfa` | Console users without MFA |
| `GET` | `/api/v1/iam/risky-policies` | Customer policies with `Action *` on `Resource *` |
| `GET` | `/api/v1/iam/overprivileged-custom-policies` | Customer policies with service wildcards (`s3:*`, `iam:*`, etc.) |
| `GET` | `/api/v1/iam/inline-policies` | Inline policies on users, groups and roles |
| `GET` | `/api/v1/iam/admin-privilege-grants` | Users, groups and roles with admin-level managed policies |
| `GET` | `/api/v1/iam/cross-account-roles` | Roles with cross-account or wildcard trust policies |
| `GET` | `/api/v1/iam/root-account-status` | Root MFA, access keys and signing certificates |

### Lambda, ELB, ECR, ACM
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/lambda/public-functions?region=` | Functions with public URL or resource policy |
| `GET` | `/api/v1/elb/public-listeners?region=` | Internet-facing load balancers with public listener risks |
| `GET` | `/api/v1/ecr/repository-risks?region=` | Repositories with public access or scan-on-push disabled |
| `GET` | `/api/v1/acm/expiring-certificates?region=&days=30` | Certificates expired or expiring within N days |

### Inspector, audits, cost
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/inspector/vulnerabilities?region=&resourceType=&severity=&status=` | Inspector findings (`resourceType`: `ec2`, `lambda`, `ecr`) |
| `GET` | `/api/v1/audits/untagged-resources?region=` | Resources missing the configured project tag |
| `GET` | `/api/v1/audits/resources-by-project?region=&projectTagValue=` | Resources matching a project tag value |
| `GET` | `/api/v1/cost/by-project?startDate=&endDate=` | Costs grouped by project tag (`yyyy-MM-dd`, inclusive) |

## Contributing 🤝
1. Fork the repository
2. Create a branch (`feature/your-feature`)
3. Open a Pull Request describing your changes

## License 📜
Created by Agustina Fassina.
# Aws.Api (.NET 10) ☁️
Security-focused Web API for AWS. It connects to your AWS account and surfaces resources that may not follow cloud security best practices.

**Status:** early stage — S3 public bucket scanning is available today. More checks are planned.

## Vision 🎯
This API is meant to help teams audit and improve their AWS posture by exposing actionable findings: misconfigurations, risky permissions, stale credentials, and other gaps against well-known best practices.

### Roadmap 🗺️
| Area | Planned checks |
|------|----------------|
| **S3** | Public buckets *(available)* |
| **IAM** | Users without MFA, access keys that need rotation |
| **IAM** | Roles and permissions aligned with least privilege |
| **EC2** | Instances and exposure / vulnerability signals |
| **Containers** | Docker images and related vulnerability insights |
| **Cost** | Cost visibility and optimization hints |
| **More** | Additional security and governance checks over time |

## Requirements ✅
- .NET 10 SDK
- AWS credentials with permissions to read the resources being scanned
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

In Development, Swagger is available at `http://localhost:5142/swagger`.

Health check: `GET /health`

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
The API uses the [AWS SDK default credential chain](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/creds-locate.html):

- Environment variables: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN`
- Shared credentials file (`~/.aws/credentials`)
- IAM role (when running on AWS compute)

Default region is set in `appsettings.json`:

```json
"AWS": {
  "Region": "us-east-1"
}
```

Override via environment variable: `AWS__Region=us-east-1`

## Contributing 🤝
1. Fork the repository
2. Create a branch (`feature/your-feature`)
3. Open a Pull Request describing your changes

## License 📜
Created by Agustina Fassina.

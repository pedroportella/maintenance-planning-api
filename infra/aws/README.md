# AWS Review Terraform

This Terraform stack defines review infrastructure for the synthetic maintenance-planning showcase. It is intentionally account-neutral: backend configuration, image digests, runtime secret values and generated plans stay local.

## Layout

- `versions.tf`, `variables.tf`, `main.tf` and `outputs.tf` define the root stack.
- `modules/network` creates the VPC, subnet tiers, optional private egress and security groups.
- `modules/ecr` creates immutable image repositories with scan-on-push.
- `modules/edge` creates the application load balancer, target groups and optional HTTPS/WAF wiring.
- `modules/database` creates private SQL Server infrastructure with managed master password.
- `modules/secrets` creates secret placeholders without committing values.
- `modules/messaging` creates the event bus, work queue and dead-letter queue for later event ingestion.
- `modules/observability` creates workload log groups.
- `modules/identity` creates ECS execution and task roles.
- `modules/app` creates the ECS cluster, API service and optional web service.
- `modules/worker` creates worker, migration and simulator task definitions, plus an optional simulator schedule.
- `modules/budget` creates the monthly review budget alert when emails are provided.

## Validate

```bash
node scripts/terraform-validate.mjs
```

This runs format, backend-disabled init and validate.

## Backend

Copy the example backend file locally and edit it for the review account:

```bash
cp infra/aws/backend.example.hcl infra/aws/backend.hcl
terraform -chdir=infra/aws init -backend-config=backend.hcl
```

Do not commit the edited backend config.

## Deployment Inputs

Copy `terraform.tfvars.example` to a local variable file and replace placeholder image digests with values pushed to the review ECR repositories. Keep secret values in Secrets Manager, not in Terraform variables.

Use `web_data_mode = "backend"` only when the server-side web-to-API route is ready and `web_backend_api_url` is set to an internal or review URL. Keep the API URL server-only.

## Migration Boundary

The migration task definition, role, log group, security group and database secret reference are present so release orchestration has an infrastructure target. Terraform must not run schema migrations.

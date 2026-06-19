# AWS And Terraform

Terraform under [infra/aws](../infra/aws/) defines a review environment for the synthetic maintenance-planning showcase. It is designed to be planned and reviewed before any apply, and it keeps account values, generated plans, local backend files and secrets out of the repository.

## Review Infrastructure

The review shape includes:

- a tagged VPC with public load-balancer subnets, private service subnets and isolated database subnets;
- optional private service endpoints or NAT-based egress for image pulls, logs and secrets access;
- ECR repositories for API, web, simulator, worker and migration-runner images with immutable tags and scan-on-push;
- an ECS/Fargate cluster with API and web services using digest-based image references;
- one-off task definitions for migration, simulator and worker workloads;
- an application load balancer with API path routing and optional HTTPS/WAF inputs;
- RDS SQL Server with encrypted storage, private networking and RDS-managed master password;
- Secrets Manager placeholders for runtime database passwords and simulator API token values;
- EventBridge, SQS and DLQ resources for the later event path;
- CloudWatch log groups and an optional budget alert.

All resources carry these tags:

```text
project=maintenance-planning-prototype
owner=pedro
environment=review
```

## Remote Backend Bootstrap

Create the remote backend bucket and lock table outside this Terraform stack before the first long-running plan or apply. Keep the backend names local to the AWS account and do not commit account-specific values.

Use [infra/aws/backend.example.hcl](../infra/aws/backend.example.hcl) as a local template:

```bash
cp infra/aws/backend.example.hcl infra/aws/backend.hcl
```

Edit the local copy with the review account backend names, then initialize with:

```bash
terraform -chdir=infra/aws init -backend-config=backend.hcl
```

`backend.hcl` is intentionally local-only and should not be committed.

## Local Validation

Validation does not require AWS credentials when backend initialization is disabled:

```bash
node scripts/terraform-validate.mjs
```

Equivalent commands:

```bash
terraform -chdir=infra/aws fmt -check -recursive
terraform -chdir=infra/aws init -backend=false -input=false
terraform -chdir=infra/aws validate -no-color
```

## Image Inputs

Task definitions take immutable image digests:

```text
api_image_digest
web_image_digest
simulator_image_digest
worker_image_digest
migration_image_digest
```

Each value must be formatted as `sha256:<digest>`. The Terraform combines those digests with the matching ECR repository URL. Do not deploy tag-only task definitions.

## Deploy Review

Before applying:

1. Confirm the monthly budget alert has at least one email subscriber.
2. Confirm backend config is local and points at the intended review account.
3. Push API, web and simulator images to the review ECR repositories and record their digests.
4. Populate Secrets Manager values for API database password, migration database password and simulator API token when those workloads are used.
5. Choose either NAT egress or private service endpoints for private tasks.
6. Keep `MAINTENANCE_PLANNING_API_URL` server-only for web backend mode. Do not add browser-visible backend URL variables.

Review a plan before apply:

```bash
terraform -chdir=infra/aws plan -var-file=review.auto.tfvars
```

Apply only after the plan has been reviewed:

```bash
terraform -chdir=infra/aws apply -var-file=review.auto.tfvars
```

## Smoke Checks

After apply, check:

```bash
curl -fsS http://<review-load-balancer>/health/startup
curl -fsS http://<review-load-balancer>/health/live
curl -fsS http://<review-load-balancer>/health/ready
curl -fsS http://<review-load-balancer>/openapi/v1.json
```

Then run the migration task through release orchestration, not through Terraform provisioners. After migration success, check protected API routes with the configured review token and run the simulator task only when its token and API URL have been configured.

## Teardown

Destroy review infrastructure when it is no longer needed:

```bash
terraform -chdir=infra/aws destroy -var-file=review.auto.tfvars
```

Post-destroy checks:

- confirm the ECS services and one-off task runs have stopped;
- confirm the database instance and load balancer are gone;
- confirm no review image repositories, log groups or queues remain unless intentionally retained;
- confirm the budget alert no longer reports ongoing review spend.

## Remaining Review Gaps

- HTTPS, WAF, DNS and stricter ingress depend on review account inputs.
- Application and migration database users still need a controlled credential creation and rotation path.
- The migration-runner image and release gate are implemented in the next infrastructure stage; this stack only defines the task infrastructure.
- Event ingestion and simulator AWS publish mode are later stages; messaging resources are present so IAM, queues and logs have a stable target.

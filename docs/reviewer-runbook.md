# Reviewer Runbook

Current state: .NET API and event ingestion worker with health endpoints, OpenAPI JSON, safe errors, tests, SQL Server persistence through EF Core migrations, local HTTP import contracts for synthetic source-system-shaped events, planner work-order query routes, local review auth policies, command rate limiting, EventBridge/SQS review wiring and containerised API, worker and migration-runner runtime paths.

## Local Checks

```bash
dotnet format MaintenancePlanning.sln --verify-no-changes --no-restore
dotnet test MaintenancePlanning.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
node scripts/quality-guards.mjs all
node scripts/reviewer-evidence-smoke.mjs
node scripts/terraform-validate.mjs
node scripts/ecs-release-gate-tests.mjs
npm run deploy:release-gate:dry-run
node scripts/container-smoke.mjs
node scripts/worker-container-build.mjs
node scripts/database-smoke.mjs
```

## Database Smoke

```bash
node scripts/database-smoke.mjs
```

The smoke starts an isolated local SQL Server compose project, applies EF Core migrations explicitly, starts the API with database configuration, checks `/health/ready`, checks protected `/api/v1/operations/migration-readiness` with the synthetic reviewer token, then removes the isolated compose resources.

## Container Smoke

```bash
node scripts/container-build.mjs
node scripts/container-smoke.mjs --skip-build
```

The smoke checks startup, liveness, readiness, final image contents and graceful container stop behaviour.

The worker image can be built locally without AWS credentials:

```bash
node scripts/worker-container-build.mjs
```

## Terraform Review

```bash
node scripts/terraform-validate.mjs
```

For a review deployment, create a local backend config from `infra/aws/backend.example.hcl`, populate a local variable file with image digests and budget alert email, then review a plan before any apply:

```bash
terraform -chdir=infra/aws init -backend-config=backend.hcl
terraform -chdir=infra/aws plan -var-file=review.auto.tfvars
```

Terraform defines infrastructure and task definitions only. It does not execute database migrations. The migration task should be run by release orchestration after the migration-runner image exists. EventBridge, SQS, the dead-letter queue and the worker service definition are provisioned for review, but a live synthetic event publish has not been run from this repository state.

## Migration Release Gate

```bash
node scripts/build-migration-bundle.mjs
node scripts/migration-container-build.mjs
node scripts/ecs-release-gate-tests.mjs
npm run deploy:release-gate:dry-run
```

For a review deployment, push the API and migration-runner images, render task-definition JSON with exact image digests, then run [the migration release gate](release-gate.md). The gate runs the migration task in private subnets with public IP assignment disabled and updates the API service only after the named migration container exits successfully.

After a review apply, check the load-balancer health and OpenAPI routes, then run protected API and simulator smokes only after runtime secrets have been populated. Destroy the review stack when it is no longer being used and confirm services, database, queues, logs and image repositories are no longer creating review spend.

## Future Local Smoke

The planned local smoke will:

1. wait for API readiness;
2. verify SQL Server readiness;
3. post a deterministic synthetic scenario to the local import endpoint;
4. retry the import and confirm idempotency;
5. start a planning run;
6. fetch work-order backlog and recommendations;
7. fetch operations posture.

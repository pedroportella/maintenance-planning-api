# Reviewer Runbook

Current state: .NET API and event ingestion/outbox worker with health endpoints, OpenAPI JSON, safe errors, tests, SQL Server persistence through EF Core migrations, local HTTP import contracts for synthetic source-system-shaped events, planner work-order query routes, local review auth policies, command rate limiting, operations-protected dead-letter replay, EventBridge/SQS review wiring, outbound EventBridge dispatch, a simulator EventBridge publish task definition and containerised API, worker and migration-runner runtime paths.

## Cross-Repo Review Path

This API is the system-of-record side of a three-repo synthetic showcase:

- [maintenance-data-simulator](https://github.com/pedroportella/maintenance-data-simulator) seeds deterministic local data and can publish the same synthetic scenario events to EventBridge when a review AWS stack is ready.
- [maintenance-planning-web](https://github.com/pedroportella/maintenance-planning-web) is the planner workbench that reads this API through server-side backend mode or deterministic mock mode.

Smallest credible live AWS review path:

1. Build and push API, worker, migration-runner, simulator and web images, then record exact image digests.
2. Review the Terraform plan with digest-pinned task definitions, budget controls, secret placeholders, queues, worker wiring, EventBridge rules and teardown notes.
3. Run the migration release gate in dry-run mode, then run the live migration task only after database credentials and task networking are confirmed.
4. Check API and web health through the review endpoints without exposing private backend origins to the browser.
5. Publish the deterministic simulator scenario to EventBridge with explicit confirmation.
6. Confirm worker consumption into SQL Server projections and verify idempotent retry behaviour.
7. Check operations posture for freshness, queue depth and dead-letter state.
8. If the review stack is safe to mutate, run a protected dead-letter replay smoke and one outbound event dispatch smoke with synthetic records.

No live AWS deployment, simulator publish, worker consumption, SQL projection, dead-letter replay or outbound EventBridge smoke has been run from this repository state.

## Local Checks

```bash
dotnet format MaintenancePlanning.sln --verify-no-changes --no-restore
dotnet test MaintenancePlanning.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
node scripts/quality-guards.mjs all
node scripts/reviewer-evidence-smoke.mjs
node scripts/event-contract-smoke.mjs
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

Terraform defines infrastructure and task definitions only. It does not execute database migrations. The migration task should be run by release orchestration after the migration-runner image exists. EventBridge, SQS, the dead-letter queue, the worker service definition, outbound dispatch permissions and the simulator publish task are provisioned for review, but live synthetic event publish, dead-letter replay and outbound publish smokes have not been run from this repository state.

## Migration Release Gate

```bash
node scripts/build-migration-bundle.mjs
node scripts/migration-container-build.mjs
node scripts/ecs-release-gate-tests.mjs
npm run deploy:release-gate:dry-run
```

For a review deployment, push the API and migration-runner images, render task-definition JSON with exact image digests, then run [the migration release gate](release-gate.md). The gate runs the migration task in private subnets with public IP assignment disabled and updates the API service only after the named migration container exits successfully.

After a review apply, check the load-balancer health and OpenAPI routes, then run protected API, worker and simulator smokes only after required runtime secrets have been populated. Destroy the review stack when it is no longer being used and confirm services, database, queues, logs and image repositories are no longer creating review spend.

## Future Local Smoke

The planned local smoke will:

1. wait for API readiness;
2. verify SQL Server readiness;
3. post a deterministic synthetic scenario to the local import endpoint;
4. retry the import and confirm idempotency;
5. start a planning run;
6. fetch work-order backlog and recommendations;
7. fetch operations posture.

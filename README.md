# maintenance-planning-api

Neutral review prototype for a production-shaped maintenance-planning API.

## What This Is

This repository will contain a .NET API and worker service for synthetic maintenance-planning workflows:

- source-system-shaped work-order import contracts using synthetic data;
- SQL Server persistence through EF Core;
- idempotent imports and event processing;
- planning runs and explainable work-order package recommendations;
- operations posture, health/readiness and safe API errors;
- Terraform-managed AWS review infrastructure.

## Boundary

This is a prototype for review and learning. It does not connect to any employer, client or production source system. All data is synthetic, and production concerns such as enterprise identity, high availability, formal security assurance and production support remain production-next work unless explicitly implemented.

## Start Here

- [Architecture](docs/architecture.md)
- [API](docs/api.md)
- [Containerisation](docs/containerisation.md)
- [Event contracts](docs/event-contracts.md)
- [AWS and Terraform](docs/aws-terraform.md)
- [Migration release gate](docs/release-gate.md)
- [Security and operations](docs/security-and-operations.md)
- [Reviewer runbook](docs/reviewer-runbook.md)
- [Production-next](docs/production-next.md)

## Current State

The repository now contains the initial .NET API, worker and test solution skeleton, a containerised API runtime path, SQL Server persistence through EF Core migrations, local HTTP import contracts for synthetic source-system-shaped work orders and maintenance events, planner work-order query routes, planning-run recommendation routes, Terraform review-infrastructure foundations and an ECS migration release-gate script. Implemented foundation capabilities include startup, liveness and readiness health endpoints, OpenAPI JSON, local bearer-token auth policies, command rate limiting, migration readiness reporting, idempotent import audit fields, deterministic package recommendations, planner decision audit rows, correlation ids, safe problem-details errors, structured console logging, graceful shutdown state, explicit local migrations, a restricted container smoke, Terraform validation and dry-run release-gate checks.

## Run Locally

```bash
dotnet run --project src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj
```

Useful local endpoints:

- `GET /health/startup`
- `GET /health/live`
- `GET /health/ready`
- `GET /openapi/v1.json`
- `GET /api/v1/operations/migration-readiness`
- `GET /api/v1/operations/posture`
- `GET /api/v1/work-orders`
- `GET /api/v1/work-orders/{id}`
- `POST /api/v1/imports/source-work-orders`
- `POST /api/v1/imports/maintenance-events`
- `POST /api/v1/planning-runs`
- `GET /api/v1/planning-runs/{id}`
- `GET /api/v1/planning-runs/{id}/recommendations`
- `POST /api/v1/packages/{id}/decisions`

Local `/api/v1` routes require a synthetic bearer token such as `local-reviewer-token`. Health and OpenAPI routes stay public for readiness checks.

To run with local SQL Server:

```bash
dotnet tool restore
cp .env.local.example .env.local
docker compose --env-file .env.local --profile sqlserver up -d sqlserver
set -a
. ./.env.local
set +a
dotnet dotnet-ef database update --project src/MaintenancePlanning.Infrastructure/MaintenancePlanning.Infrastructure.csproj --startup-project src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj --context MaintenancePlanningDbContext
dotnet run --project src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj
```

The API reports pending migrations but does not apply schema changes during startup.

## Checks

```bash
dotnet format MaintenancePlanning.sln --verify-no-changes --no-restore
dotnet test MaintenancePlanning.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
node scripts/quality-guards.mjs all
node scripts/terraform-validate.mjs
node scripts/reviewer-evidence-smoke.mjs
node scripts/ecs-release-gate-tests.mjs
npm run deploy:release-gate:dry-run
node scripts/container-smoke.mjs
node scripts/database-smoke.mjs
```

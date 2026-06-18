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
- [Event contracts](docs/event-contracts.md)
- [AWS and Terraform](docs/aws-terraform.md)
- [Security and operations](docs/security-and-operations.md)
- [Reviewer runbook](docs/reviewer-runbook.md)
- [Production-next](docs/production-next.md)

## Current State

The repository now contains the initial .NET API, worker and test solution skeleton. Implemented foundation capabilities include startup, liveness and readiness health endpoints, OpenAPI JSON, correlation ids, safe problem-details errors, structured console logging and graceful shutdown state.

## Run Locally

```bash
dotnet run --project src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj
```

Useful local endpoints:

- `GET /health/startup`
- `GET /health/live`
- `GET /health/ready`
- `GET /openapi/v1.json`

## Checks

```bash
dotnet format MaintenancePlanning.sln --verify-no-changes --no-restore
dotnet test MaintenancePlanning.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
node scripts/quality-guards.mjs all
node scripts/reviewer-evidence-smoke.mjs
```

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Context

This is a **synthetic** maintenance-planning prototype — a .NET 8 API and worker that simulate planning workflows for industrial assets (work orders, functional locations, major events). No real source-system connectivity or production identity exists. All data is synthetic.

It is one of three coordinated repos:
- **This repo** — API + Worker + Infrastructure
- `maintenance-data-simulator` — Sends synthetic events
- `maintenance-planning-web` — Frontend UI

**Production-shaped, not production-ready.** Structured for review evidence; production hardening (resilience, full security review, observability, identity) is future work.

## Commands

### .NET (primary build/test tool)

```bash
# Run all tests
dotnet test MaintenancePlanning.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false

# Run a single test class or method
dotnet test MaintenancePlanning.sln --filter "FullyQualifiedName~<TestClassName>"

# Check formatting (CI enforcement)
dotnet format MaintenancePlanning.sln --verify-no-changes --no-restore

# Run the API locally (no database)
dotnet run --project src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj

# Apply EF migrations
dotnet ef database update --project src/MaintenancePlanning.Infrastructure --startup-project src/MaintenancePlanning.Api
```

### Node.js scripts (quality gates and container tooling)

```bash
npm run guard                      # Quality guards (formatting, build, test)
npm run test:reviewer-evidence     # Smoke tests against a running container
npm run test:event-contracts       # Validate AsyncAPI event contract tests
npm run verify                     # Full suite: guards + smoke + event contracts

npm run db:migrate                 # Run EF migrations via bundled executable
npm run db:smoke                   # Smoke test database connectivity

npm run container:build            # Build API Docker image
npm run container:run              # Run API container locally
npm run container:smoke            # Smoke test the running container

npm run migration:bundle           # Bundle EF migrations as standalone executable
npm run infra:validate             # Validate Terraform (format, init, validate)

npm run deploy:release-gate:dry-run   # Dry-run ECS release gate check
npm run release:gate:test             # Release gate integration test
```

### Local database (SQL Server via Docker)

```bash
# Copy env file and edit password/port
cp .env.local.example .env

# Start SQL Server
docker compose --profile sqlserver up -d

# Then apply migrations
npm run db:migrate
```

## Architecture

### Solution Structure

```
src/
  MaintenancePlanning.Api           # ASP.NET Core web app — entry point
  MaintenancePlanning.Application   # Use cases, services, business logic
  MaintenancePlanning.Domain        # Entities, enums, domain rules
  MaintenancePlanning.Infrastructure # EF Core DbContext, AWS SDK adapters
  MaintenancePlanning.Worker        # Console app — async SQS/EventBridge worker
tests/
  MaintenancePlanning.Api.Tests     # xUnit tests (endpoints, services, DB smoke)
infra/aws/                          # Terraform modules for AWS review environment
deploy/release/                     # ECS task definition examples
docs/                               # Architecture, API, contracts, runbooks
scripts/                            # Node.js quality gate and container scripts
```

### Layered Architecture (Clean Architecture)

**Domain** → **Application** → **Infrastructure** / **API** / **Worker**

- **Domain**: Entities (`Asset`, `WorkOrder`, `MajorEvent`, `PlanningRun`, `WorkOrderPackage`, `PlannerDecision`, `OutboxEvent`, etc.) and enums. No dependencies on other layers.
- **Application**: `ImportService`, `PlanningService`, `EventIngestionService`, `OutboxDispatcher`, `DeadLetterReplayService`. Orchestrates domain logic.
- **Infrastructure**: EF Core (`MaintenancePlanningDbContext`) with SQL Server; AWS SDK adapters for EventBridge and SQS.
- **API** (`Program.cs` → `ApiApplication.Create()`): Minimal APIs grouped by feature — `HealthEndpoints`, `ImportEndpoints`, `PlanningEndpoints`, `WorkOrderEndpoints`, `OperationsEndpoints`. Runs on port 5000 (local) or 8080 (container).
- **Worker** (separate console app): `EventIngestionWorker` (SQS → application), `OutboxDispatchWorker` (outbox → EventBridge).

### Key Patterns

- **Transactional Outbox**: Events are written to `OutboxEvent` table inside the same DB transaction, then dispatched async to EventBridge by `OutboxDispatchWorker`.
- **Idempotency**: Imports tracked via `IntegrationImport` records; duplicate import keys are rejected.
- **Bearer token auth**: `TestTokenAuthenticationHandler` — synthetic local tokens only; policy-based (`planner`, `import`, `operations` roles).
- **Problem Details (RFC 7807)**: All error responses include correlation IDs.
- **Health probes**: `/health/startup`, `/health/live`, `/health/ready` — Kubernetes-style.
- **Rate limiting**: Configurable permit limit/window via `MaintenancePlanning__Security__CommandRateLimit__*` env vars.

### Data Flow

```
Synthetic source (simulator or direct HTTP)
  → POST /api/imports  or  SQS via Worker
  → Application services
  → SQL Server (EF Core)
  → Planning run → WorkOrderPackages + PlannerDecisions
  → OutboxEvent records
  → EventBridge (async, OutboxDispatchWorker)
```

### AWS Infrastructure (Terraform)

Modules in `infra/aws/`: network, ECR, edge (ALB), database (RDS SQL Server), secrets (SSM), messaging (EventBridge + SQS), observability, identity (IAM), app (ECS Fargate API), worker (ECS Fargate), budget. No state, plans, or secrets committed. Validated but not claimed live until evidence is generated.

### Build Constraints

- `Directory.Build.props`: `net8.0`, nullable enabled, `TreatWarningsAsErrors=true`, latest C# language version.
- `global.json`: Pinned to .NET SDK 8.0.128.
- `.config/dotnet-tools.json`: Pinned `dotnet-ef` 8.0.28.
- Node.js 22 / pnpm 10.18.3 for scripts.

## Conventions

- **Synthetic data only**: never introduce real branding, real asset IDs, or real industry terminology.
- **Backend-authoritative**: all validation, idempotency, and business rules live server-side.
- **Preserve source IDs and idempotency keys** when processing imported events.
- **Smoke checks are load-bearing**: do not remove or weaken `npm run container:smoke` or `npm run db:smoke`.
- **Async paths (EventBridge/SQS/DLQ replay)**: implemented and tested but live AWS evidence may still be pending.

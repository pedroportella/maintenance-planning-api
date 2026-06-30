# Maintenance Planning API

Working .NET API and worker prototype for a synthetic maintenance-planning solution.

This repository is the system-of-record side of a three-repo review slice. It imports source-system-shaped maintenance events, persists planning projections in SQL Server, creates explainable work-order package recommendations, records planner decisions and exposes operations posture for review.

It is a neutral prototype using synthetic data only. It does not connect to any employer, client or production source system, and it does not claim production support, high availability, production identity or formal assurance.

## Fast Review

1. Read the [solution handover](docs/solution-handover.md) for the whole API, simulator and web review path.
2. Scan [solution architecture](docs/solution-architecture.md) for component boundaries and local/AWS/production-next diagrams.
3. Use [reviewer-runbook.md](docs/reviewer-runbook.md) for API checks, Terraform review, migration release-gate evidence and AWS smoke sequencing.
4. Use [local-docker-system.md](docs/local-docker-system.md) for the proven local Docker path across API, simulator, SQL Server and backend-mode web.
5. Treat AWS EventBridge, SQS worker ingestion, DLQ replay and outbound EventBridge smoke as pending until live review evidence is captured.

## Evidence Status

| Area | Current status |
| --- | --- |
| API, worker, persistence and protected operations | Implemented with local automated tests and reviewer-evidence guards. |
| Local Docker API, simulator, SQL Server and backend-mode web path | Proven by the local Docker system runbook. |
| Terraform, EventBridge, SQS, DLQ and release-gate infrastructure | Defined for review, with dry-run and validation helpers. |
| Live AWS deployment, worker consumption, DLQ replay and outbound EventBridge smoke | Pending until a short-lived review stack is applied, smoked and torn down. |

## Runtime Policy

The repository currently targets .NET 8 LTS through `Directory.Build.props`, pins the SDK in `global.json` with `rollForward: latestPatch`, uses .NET 8 EF tools and builds API, worker and migration-runner images from the matching `8.0-bookworm-slim` Microsoft images.

Runtime upgrades are treated as explicit engineering changes, not reviewer-documentation cleanup. Use the [runtime upgrade policy](docs/runtime-upgrade-policy.md) before changing the target framework, SDK roll-forward, EF tools, Docker base images, CI checks, migration bundle or release-gate flow.

## Showcase Repositories

| Repository | Responsibility |
| --- | --- |
| [maintenance-planning-api](https://github.com/pedroportella/maintenance-planning-api) | Persistence, API contracts, recommendations, decisions, operations posture, worker, replay, outbound events and review infrastructure. |
| [maintenance-data-simulator](https://github.com/pedroportella/maintenance-data-simulator) | Deterministic synthetic source-system-shaped scenarios, local HTTP feed and explicit EventBridge publish mode. |
| [maintenance-planning-web](https://github.com/pedroportella/maintenance-planning-web) | Planner workbench over typed service adapters, with deterministic mock mode and server-side backend mode. |

## Repository Shape

```text
src/       .NET API, application, domain, infrastructure and worker projects
tests/     focused API, application and infrastructure tests
deploy/    ECS release-gate task definition examples
docs/      architecture, runbooks, security, event and production-next notes
infra/     Terraform review infrastructure
scripts/   quality guards, smoke checks and release-gate helpers
```

## What Is Real

- .NET API routes for health, OpenAPI, imports, work orders, planning runs, recommendations, planner decisions and protected operations.
- SQL Server persistence through explicit EF Core migrations.
- Backend-authoritative import validation, idempotency and import audit.
- Deterministic package recommendations with blocker explanations and source-data readiness.
- Audited planner decisions and transactional outbound event outbox records.
- Operations posture for source freshness, queue depth, dead-letter state and latest ingestion failure.
- Protected dead-letter replay command with audit records.
- Event ingestion worker and at-least-once outbound EventBridge dispatch paths.
- API, worker and migration-runner container paths.
- Terraform review infrastructure, digest-pinned task definition support and migration release-gate scripts.
- Public documentation guards, event-contract smoke checks and reviewer-evidence smoke checks.

## What Is Synthetic Or Prototype-Only

- All work orders, maintenance events, planner decisions and scenario outcomes are synthetic.
- Local bearer tokens are review-only and replace production identity.
- The local HTTP import path is source-system-shaped, not a real source-system connection.
- Terraform defines a review environment, but live AWS deployment and smoke evidence are separate from this README.
- EventBridge, SQS worker ingestion, DLQ replay and outbound EventBridge smoke are not claimed as exercised until a live review stack proves them.
- Planning-run creation supports client-supplied idempotency for safe local retry checks.
- Production controls such as enterprise identity, restore drills, incident ownership, full observability, independent security review and resilience assurance remain production-next work.

## API Surface

```text
GET  /health/startup
GET  /health/live
GET  /health/ready
GET  /openapi/v1.json
GET  /api/v1/operations/migration-readiness
GET  /api/v1/operations/posture
POST /api/v1/operations/eventing/dead-letter-replays
GET  /api/v1/work-orders
GET  /api/v1/work-orders/{id}
POST /api/v1/imports/source-work-orders
POST /api/v1/imports/maintenance-events
POST /api/v1/planning-runs
GET  /api/v1/planning-runs/{id}
GET  /api/v1/planning-runs/{id}/recommendations
POST /api/v1/packages/{id}/decisions
```

Health and OpenAPI routes are public for local readiness checks. `/api/v1` routes require synthetic local bearer tokens such as `local-reviewer-token`.

## Run Locally

Start the API without local SQL Server when you only need health, OpenAPI and safe unavailable-database responses:

```bash
dotnet run --project src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj
```

Run with local SQL Server persistence:

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

Open:

- Health: `http://localhost:5000/health/ready`
- OpenAPI: `http://localhost:5000/openapi/v1.json`

The API reports pending migrations but does not apply schema changes during startup. Use the [local Docker system runbook](docs/local-docker-system.md) for the full API, simulator, SQL Server and backend-mode web review path.

## Verify

Focused API checks:

```bash
dotnet format MaintenancePlanning.sln --verify-no-changes --no-restore
dotnet test MaintenancePlanning.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
npm run guard
npm run test:reviewer-evidence
npm run test:event-contracts
npm run release:gate:test
```

Broader review helpers:

```bash
npm run verify
npm run deploy:release-gate:dry-run
node scripts/terraform-validate.mjs
node scripts/container-smoke.mjs
node scripts/worker-container-build.mjs
node scripts/database-smoke.mjs
```

Use Docker and database smokes only when container packaging or persistence is part of the review.

## Key Docs

- [Solution handover](docs/solution-handover.md)
- [Reviewer runbook](docs/reviewer-runbook.md)
- [Architecture](docs/architecture.md)
- [Solution architecture](docs/solution-architecture.md)
- [API details](docs/api.md)
- [Event contracts](docs/event-contracts.md)
- [Security and operations](docs/security-and-operations.md)
- [Local Docker system runbook](docs/local-docker-system.md)
- [Containerisation](docs/containerisation.md)
- [AWS and Terraform](docs/aws-terraform.md)
- [Migration release gate](docs/release-gate.md)
- [Runtime upgrade policy](docs/runtime-upgrade-policy.md)
- [Production-next](docs/production-next.md)

## Production Next

- Replace local synthetic tokens with validated production identity and authorization.
- Connect through a governed integration boundary to a real maintenance source or curated data platform.
- Add private networking, environment separation, backup and restore drills.
- Add full logs, metrics, traces, alert routing and support ownership.
- Add SBOMs, provenance attestations, image signing and registry vulnerability scanning.
- Complete threat modelling, independent security review, resilience testing and cost-management processes.

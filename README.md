# Maintenance Planning API

Working .NET API and worker prototype for a synthetic maintenance-planning solution.

This repository is the system-of-record side of a three-repo review slice. It imports source-system-shaped maintenance events, persists planning projections in SQL Server, creates explainable work-order package recommendations, records planner decisions and exposes operations posture for review.

It is a neutral prototype using synthetic data only. It does not connect to any employer, client or production source system, and it does not claim production support, high availability, production identity or formal assurance.

## Fast Review

1. Read the [solution handover](docs/solution-handover.md) for the whole API, simulator and web review path.
2. Scan [solution architecture](docs/solution-architecture.md) for component boundaries and local/AWS/production-next diagrams.
3. Use the [Planner Workbench reviewer pack](https://github.com/pedroportella/maintenance-planning-web/blob/main/docs/reviewer-pack.md) for the quickest UI review path in deterministic mock mode.
4. Use the [Simulator API reviewer runbook](https://github.com/pedroportella/maintenance-data-simulator/blob/main/docs/reviewer-runbook.md) to inspect deterministic scenarios and local feed behaviour.
5. Use [reviewer-runbook.md](docs/reviewer-runbook.md) for API checks, Terraform review, migration release-gate evidence and AWS smoke sequencing.
6. Use [local-docker-system.md](docs/local-docker-system.md) for the proven local Docker path across API, simulator, SQL Server and backend-mode web.

## Showcase Repositories

| Repository | Review role | Start here |
| --- | --- | --- |
| [maintenance-planning-api](https://github.com/pedroportella/maintenance-planning-api) | System of record and main engineering proof. | [Reviewer runbook](docs/reviewer-runbook.md) |
| [maintenance-data-simulator](https://github.com/pedroportella/maintenance-data-simulator) | Deterministic synthetic data producer. | [Simulator reviewer runbook](https://github.com/pedroportella/maintenance-data-simulator/blob/main/docs/reviewer-runbook.md) |
| [maintenance-planning-web](https://github.com/pedroportella/maintenance-planning-web) | Planner-facing workbench. | [Reviewer pack](https://github.com/pedroportella/maintenance-planning-web/blob/main/docs/reviewer-pack.md) |

## Repository Shape

```text
src/       .NET API, application, domain, infrastructure and worker projects
tests/     focused API, application and infrastructure tests
deploy/    ECS release-gate task definition examples
docs/      architecture, runbooks, security, event and production-next notes
infra/     Terraform review infrastructure
scripts/   quality guards, smoke checks and release-gate helpers
```

## Evidence Status

| Area | Status |
| --- | --- |
| Local Docker API, simulator, SQL Server and backend-mode web path | Passed on 2026-06-21. |
| Planner API contracts and operations posture | Implemented for review. |
| Planner Workbench backend mode | Implemented with server-side API configuration. |
| AWS infrastructure shape | Defined, but live evidence requires an applied review stack and smoke checks. |
| EventBridge, SQS and worker ingestion | Not proven until the live AWS event path is smoked. |
| Production-next architecture | Conceptual target only. |

## What Is Real

- .NET API routes for health, OpenAPI, imports, work orders, planning runs, recommendations, planner decisions and protected operations.
- SQL Server persistence through explicit EF Core migrations.
- Backend-authoritative import validation, idempotency and import audit.
- Deterministic package recommendations with blocker explanations and source-data readiness.
- Audited planner decisions and transactional outbound event outbox records.
- Operations posture for source freshness, queue depth, dead-letter state and latest ingestion failure.
- Protected dead-letter replay command with audit records.
- Event ingestion worker and outbound EventBridge dispatch paths.
- API, worker and migration-runner container paths.
- Terraform review infrastructure, digest-pinned task definition support and migration release-gate scripts.
- Public documentation guards, event-contract smoke checks and reviewer-evidence smoke checks.

## What Is Synthetic Or Prototype-Only

- All work orders, maintenance events, planner decisions and scenario outcomes are synthetic.
- Local bearer tokens are review-only and replace production identity.
- The local HTTP import path is source-system-shaped, not a real source-system connection.
- Terraform defines a review environment, but no live AWS deployment, simulator publish, worker consumption, SQL projection, dead-letter replay or outbound EventBridge smoke has been run from this repository state.
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

Health and OpenAPI routes are public for local readiness checks. `/api/v1` routes require synthetic local bearer tokens:

- `local-planner-token` for planner backlog, planning runs and package decisions;
- `local-import-token` for source-system-shaped import feeds;
- `local-operations-token` for operations posture and migration readiness;
- `local-reviewer-token` for reviewer smoke checks across all policies.

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
- [Production-next](docs/production-next.md)

## Production Next

- Replace local synthetic tokens with validated production identity and authorization.
- Connect through a governed integration boundary to a real maintenance source or curated data platform.
- Add private networking, environment separation, backup and restore drills.
- Add full logs, metrics, traces, alert routing and support ownership.
- Add SBOMs, provenance attestations, image signing and registry vulnerability scanning.
- Complete threat modelling, independent security review, resilience testing and cost-management processes.

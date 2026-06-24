# Solution Handover

Short handover for reviewing the Maintenance Planning Solution across the API, simulator and web repositories.

Last updated: 2026-06-24.

## What This Is

This is a three-repo synthetic maintenance-planning vertical slice:

- `Planner API (API)` owns imports, persistence, planning recommendations, planner decisions, audit, operations posture, worker ingestion, replay, outbound events and review infrastructure.
- `Simulator API (Simulator)` produces deterministic source-system-shaped events for local HTTP feed checks and explicit EventBridge publish checks.
- `Planner Workbench (Web)` presents the planner workflow through deterministic mock mode by default and server-side backend mode when the API has been seeded. Its current UI is assembled through a Radix-backed local adapter system with Sass theme entrypoints and separated component and route visual evidence.

It is production-shaped review evidence, not a production service. It does not connect to any employer, client or production source system, and it does not claim production support, high availability, production identity or formal assurance.

## Fast Review Path

1. Start with the [solution architecture](solution-architecture.md) for component boundaries and deployment shapes.
2. Use the [Planner Workbench reviewer pack](https://github.com/pedroportella/maintenance-planning-web/blob/main/docs/reviewer-pack.md) for the quickest UI review path in deterministic mock mode.
3. Use the [Simulator API reviewer runbook](https://github.com/pedroportella/maintenance-data-simulator/blob/main/docs/reviewer-runbook.md) to inspect deterministic scenarios and local feed behaviour.
4. Use the [Planner API reviewer runbook](reviewer-runbook.md) for API, persistence, worker, Terraform and migration-release evidence.
5. Use the [local Docker system runbook](local-docker-system.md) when reviewing the proven local API, simulator, SQL Server and backend-mode web path.
6. Treat AWS as pending evidence until a live review stack is applied, smoked and torn down.

## Repositories

| Repository | Review role | Start here |
| --- | --- | --- |
| [maintenance-planning-api](https://github.com/pedroportella/maintenance-planning-api) | System of record and main engineering proof. | [Reviewer runbook](reviewer-runbook.md) |
| [maintenance-data-simulator](https://github.com/pedroportella/maintenance-data-simulator) | Deterministic synthetic data producer. | [Simulator reviewer runbook](https://github.com/pedroportella/maintenance-data-simulator/blob/main/docs/reviewer-runbook.md) |
| [maintenance-planning-web](https://github.com/pedroportella/maintenance-planning-web) | Planner-facing workbench. | [Reviewer pack](https://github.com/pedroportella/maintenance-planning-web/blob/main/docs/reviewer-pack.md) |

## Review Commands

Fast standalone workbench review:

```sh
cd maintenance-planning-web
pnpm install
pnpm guard
pnpm check
pnpm test:reviewer-pack
pnpm test:reviewer-evidence
```

Synthetic scenario review:

```sh
cd maintenance-data-simulator
pnpm install
pnpm verify
pnpm simulator generate --scenario baseline-week
pnpm simulator feed --scenario baseline-week --dry-run
```

API evidence checks:

```sh
cd maintenance-planning-api
npm run guard
npm run test:reviewer-evidence
npm run test:event-contracts
npm run release:gate:test
```

Use the repo-level runbooks for full Docker, SQL Server, backend-mode web and AWS preparation commands.

## What Is Real

- .NET API routes for health, OpenAPI, imports, planning runs, recommendations, planner decisions, work orders and protected operations.
- SQL Server persistence through explicit EF Core migrations.
- Idempotent import handling, recommendation generation, planner decision audit and transactional outbox records.
- Worker, EventBridge, SQS, DLQ and outbound dispatch code paths prepared for review infrastructure.
- Deterministic simulator scenario packs, schema validation, local feed mode, API smoke and confirmation-gated EventBridge publish mode.
- Next.js planner workbench with typed service adapters, deterministic mock mode, backend mode, Radix-backed UI adapters, reviewer pack, separated UI-library and route-wide visual evidence checks and browser-bundle leakage guards.
- Terraform review infrastructure, migration-runner task definitions and release-gate scripts.

## What Is Synthetic Or Prototype-Only

- All work orders, assets, events, planner decisions and scenario outcomes are synthetic.
- The simulator is a review data producer, not a production source-system integration.
- Local reviewer tokens and demo runtime settings are review-only.
- No production identity, production authorization model, operational support model, resilience assurance or real source-system connectivity is implemented.
- The web backend API URL and token are server-side settings only and must not be exposed through browser-visible variables.
- AWS infrastructure is defined, but live AWS evidence is still separate from local evidence.

## Evidence Status

| Area | Current status |
| --- | --- |
| API, simulator and web implementation stages | Complete for the current showcase. |
| Local Docker API, simulator, SQL Server and backend-mode web path | Passed on 2026-06-21. |
| Workbench mock review path | Implemented and guarded locally, including focused UI-library and route-wide visual baselines. |
| Terraform and migration release-gate preparation | Implemented as review infrastructure and scripts. |
| Live AWS deployment, EventBridge publish, SQS worker consumption and DLQ replay | Not yet proven from the current repository state. |

## Live AWS Evidence Path

The smallest credible live review path is:

1. Build and push API, worker, migration-runner, simulator and web images to review ECR, then record immutable digests.
2. Review a Terraform plan with digest-pinned task definitions, budget controls, secret placeholders, EventBridge, SQS, DLQ, worker wiring and teardown expectations.
3. Run the migration release gate in dry-run mode, then run the live migration task only after database credentials and private networking are confirmed.
4. Check API health, readiness, OpenAPI and web liveness through review endpoints.
5. Publish the deterministic `baseline-week` scenario from the simulator to EventBridge with explicit confirmation.
6. Verify EventBridge to SQS delivery, worker consumption into SQL Server projections and idempotent retry behaviour.
7. Check operations posture for freshness, queue depth and dead-letter state.
8. Run protected DLQ replay and outbound EventBridge smoke only when the review stack is safe to mutate.

The review stack should be short-lived and torn down after evidence capture.

## Useful Docs

- [Solution architecture](solution-architecture.md)
- [Local Docker system runbook](local-docker-system.md)
- [Planner API reviewer runbook](reviewer-runbook.md)
- [AWS and Terraform](aws-terraform.md)
- [Migration release gate](release-gate.md)
- [Security and operations](security-and-operations.md)
- [Production-next](production-next.md)

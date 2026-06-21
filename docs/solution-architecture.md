# Solution Architecture

This page describes the synthetic maintenance-planning prototype from a solution architecture point of view. It covers the three public repositories, their runtime boundaries and the difference between local Docker, AWS review and production-next shapes.

## Boundary

- The prototype uses synthetic data only.
- It does not connect to any employer, client or production source system.
- It does not claim production support, high availability, formal assurance or operational ownership.
- The AWS review shape is defined as infrastructure and scripts, but live deployment evidence is separate from this document.
- The web backend URL and API token are server-side runtime settings. They must not be exposed through browser-visible variables.

## Canonical Names

Use these names consistently across diagrams and documentation.

| Name | Repository | Responsibility |
| --- | --- | --- |
| Planner Workbench (Web) | `maintenance-planning-web` | Planner-facing Next.js workbench. It presents planner workflows over mock data by default or over server-side API calls when backend mode is configured. |
| Planner API (API) | `maintenance-planning-api` | Backend-authoritative .NET API for imports, planning runs, recommendations, planner decisions, operations posture and protected operations routes. |
| Simulator API (Simulator) | `maintenance-data-simulator` | Deterministic synthetic scenario producer for local HTTP feed checks and explicit EventBridge publish checks. |
| Worker | `maintenance-planning-api` | Event ingestion and outbox dispatch worker for the asynchronous review path. |
| Planning store | `maintenance-planning-api` | SQL Server persistence for planning projections, imports, audit, recommendations, planner decisions and outbox records. |

## System Context

```mermaid
flowchart LR
  planner["Planner / reviewer"]
  source["Future maintenance source\nor curated data platform"]
  simulator["Simulator API (Simulator)\nsynthetic source-system-shaped data"]
  product["Maintenance planning product\nplanning projection, recommendations, decisions"]
  downstream["Downstream decision consumers\nproduction-next governed boundary"]

  planner --> product
  simulator --> product
  source -. "future integration point" .-> product
  product -. "accepted / deferred / rejected decisions" .-> downstream
```

The simulator supplies synthetic data for review. A production-next version would replace the simulator in the main flow with a governed integration from an authoritative maintenance source or curated data platform.

## Logical Container View

```mermaid
flowchart LR
  browser["Planner / reviewer browser"]
  web["Planner Workbench (Web)\nNext.js server-rendered app\nserver-side API config"]
  api["Planner API (API)\nimports, queries, recommendations,\ndecisions, posture"]
  simulator["Simulator API (Simulator)\nsynthetic scenario producer"]
  worker["Worker\nevent ingestion and outbox dispatch"]
  db["Planning store\nSQL Server"]
  bus["Event bus / queue\nEventBridge + SQS in AWS review"]
  dlq["DLQ / replay boundary"]
  outbox["Outbound decision events\nproduction-next governance"]

  browser --> web
  web --> api
  simulator --> api
  simulator -. "event publish mode" .-> bus
  bus --> worker
  worker --> db
  worker --> dlq
  api --> db
  api --> outbox
```

The API owns planning truth. The workbench presents planner-facing state. The simulator produces deterministic synthetic events. The worker and queue path represent the asynchronous review architecture and must be verified separately from the local HTTP path.

## Local Docker Architecture

The local Docker system is the fast end-to-end path for the prototype. It proves local image packaging, explicit migrations, HTTP import, SQL persistence, recommendations, decisions, operations posture and backend-mode web rendering.

```mermaid
flowchart LR
  browser["Browser\nlocalhost:3104"]
  web["Planner Workbench (Web)\ncontainer\nserver-side backend mode"]
  api["Planner API (API)\ncontainer\nlocalhost:5000 or alternate port"]
  db["SQL Server container\nlocalhost:14333"]
  simulator["Simulator API (Simulator)\nshort-lived container\napi-smoke/feed"]
  migration["Migration runner\nshort-lived container"]
  notLocal["Not in local path\nEventBridge / SQS / DLQ / worker"]

  browser --> web
  web --> api
  simulator -->|"HTTP POST synthetic events"| api
  api --> db
  migration -->|"EF migrations"| db
  notLocal -. "not proven by local Docker" .-> api
```

Local Docker does not prove EventBridge delivery, SQS worker ingestion, DLQ replay, registry digest promotion, Terraform deployment or live review endpoints.

For commands, see [Local Docker system runbook](local-docker-system.md).

## AWS Review Architecture

The AWS review architecture is a cost-controlled prototype deployment shape. It is intended to resemble production boundaries without claiming production support.

```mermaid
flowchart LR
  reviewer["Reviewer browser\nsynthetic review user"]
  alb["Application Load Balancer\nWorkbench/API routing"]
  web["Planner Workbench (Web)\nECS service\nserver-side API config"]
  api["Planner API (API)\nECS service"]
  rds["RDS SQL Server\nprivate database subnets"]
  simulator["Simulator API (Simulator)\none-off ECS publish task"]
  eventbridge["EventBridge\nsynthetic event bus"]
  sqs["SQS queue"]
  worker["Worker ECS task"]
  dlq["DLQ\nfailed messages and replay"]
  migration["Migration runner\none-off ECS task"]
  ecr["ECR repositories\ndigest-pinned images"]
  secrets["Secrets Manager\nruntime credentials"]
  logs["CloudWatch logs"]
  terraform["Terraform\nreview stack and budget controls"]

  reviewer --> alb
  alb --> web
  alb --> api
  web -->|"server-side API calls"| api
  api --> rds
  migration --> rds
  simulator --> eventbridge
  eventbridge --> sqs
  sqs --> worker
  worker --> rds
  worker --> dlq
  ecr -.-> web
  ecr -.-> api
  ecr -.-> worker
  secrets -.-> web
  secrets -.-> api
  secrets -.-> worker
  logs -.-> web
  logs -.-> api
  logs -.-> worker
  terraform -.-> alb
  terraform -.-> rds
  terraform -.-> eventbridge
```

AWS review evidence requires a live run before it is described as exercised:

- images pushed with immutable digests;
- Terraform reviewed and applied with budget controls and teardown expectations;
- migrations run through the migration task;
- Planner API health, readiness and OpenAPI checked through the review endpoint;
- Planner Workbench liveness and planner routes checked through the review endpoint;
- Simulator API publishes synthetic events to EventBridge;
- EventBridge delivers to SQS;
- worker consumption into SQL Server projections is verified;
- queue depth, dead-letter posture and replay behaviour are checked only when safe.

For infrastructure details, see [AWS and Terraform](aws-terraform.md) and [Migration release gate](release-gate.md).

## Production-Next Target

Production-next is a conceptual operating model. It is not current evidence.

```mermaid
flowchart LR
  source["Authoritative maintenance source\nor curated data platform"]
  adapter["Integration adapter\nAPI / file / CDC / events"]
  bus["Event bus / queue\ndurable async handoff"]
  worker["Ingestion worker\nvalidate, map, dedupe"]
  store["Planning store\nsource IDs preserved"]
  engine["Packaging engine\nexplain blockers"]
  api["Planner API (API)\nqueries, decisions, posture"]
  web["Planner Workbench (Web)\npackage review"]
  quarantine["Dead-letter / quarantine\nreplay with controls"]
  obs["Observability\nfreshness, queues, logs, traces"]
  outbound["Outbound decisions\ngoverned integration edge"]

  source --> adapter
  adapter --> bus
  bus --> worker
  worker --> store
  store --> engine
  engine --> api
  api --> web
  worker --> quarantine
  worker --> obs
  api --> outbound
```

Production-next principles:

- the authoritative maintenance source remains the source of truth for execution data;
- the planning product owns an auditable planning projection;
- source identifiers, source timestamps, idempotency keys and import audit are preserved;
- invalid, stale or conflicting records are rejected or quarantined with review detail;
- Planner API remains backend-authoritative for recommendations and planner decisions;
- Planner Workbench presents planner review workflows over API-owned truth;
- outbound decision events require governance before they affect downstream systems.

For gap details, see [Production-next](production-next.md).

## Local HTTP Feed Flow

```mermaid
sequenceDiagram
  participant Sim as Simulator API (Simulator)
  participant Api as Planner API (API)
  participant Db as SQL Server
  participant Web as Planner Workbench (Web)

  Sim->>Api: POST synthetic maintenance events
  Api->>Db: validate, upsert, audit import
  Sim->>Api: replay same batch for idempotency check
  Api->>Db: ignore duplicates or stale records
  Sim->>Api: create planning run
  Api->>Db: write recommendations and outbox rows
  Sim->>Api: record synthetic package decision
  Api->>Db: write planner decision audit
  Web->>Api: server-side reads for planner routes
  Api-->>Web: mapped planner state
```

## Evidence Status

| Area | Status |
| --- | --- |
| Local Docker HTTP and SQL path | Proven by local smoke evidence. |
| Planner API contracts and operations posture | Implemented for review. |
| Planner Workbench backend mode | Implemented with server-side API configuration. |
| AWS infrastructure shape | Defined, but live evidence requires an applied review stack and smoke checks. |
| EventBridge, SQS and worker ingestion | Not proven until the live AWS event path is smoked. |
| Production-next architecture | Conceptual target only. |

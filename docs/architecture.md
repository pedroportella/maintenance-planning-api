# Architecture

The target architecture is a production-shaped .NET API and worker backed by SQL Server.

Planned flow:

```text
synthetic source events -> API import or EventBridge/SQS -> .NET worker/API -> SQL Server -> planning recommendations -> operations posture
```

This repository owns the API, worker, persistence, infrastructure and reviewer evidence. The source-system simulator lives in a separate repository.

Current persistence is an EF Core SQL Server model with explicit migrations. The first model separates source identifiers from product-owned planning state across assets, functional locations, work orders, major events, planning runs, packages, planner decisions, integration imports, integration events and outbox events. Work orders include readiness fields so later imports can distinguish ready backlog from source-data issues.

API startup reports migration readiness but does not apply migrations. Local development and later release orchestration apply migrations explicitly.

## Boundary

All source data is synthetic. Real source-system access, employer/client systems, production identity, production resilience and formal assurance are production-next concerns.

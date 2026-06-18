# maintenance-planning-api

Neutral review prototype for a production-shaped maintenance-planning API.

## What This Is

This repository will contain a .NET API and worker service for synthetic maintenance-planning workflows:

- SAP-shaped work-order import contracts using synthetic data;
- SQL Server persistence through EF Core;
- idempotent imports and event processing;
- planning runs and explainable work-order package recommendations;
- operations posture, health/readiness and safe API errors;
- Terraform-managed AWS review infrastructure.

## Boundary

This is a prototype for review and learning. It does not connect to any employer, client or production SAP system. All data is synthetic, and production concerns such as enterprise identity, high availability, formal security assurance and production support remain production-next work unless explicitly implemented.

## Start Here

- [Architecture](docs/architecture.md)
- [API](docs/api.md)
- [Event contracts](docs/event-contracts.md)
- [AWS and Terraform](docs/aws-terraform.md)
- [Security and operations](docs/security-and-operations.md)
- [Reviewer runbook](docs/reviewer-runbook.md)
- [Production-next](docs/production-next.md)

## Current State

Foundation guardrails only. Product code will be added in later stages.

## Checks

```bash
node scripts/quality-guards.mjs all
node scripts/reviewer-evidence-smoke.mjs
```

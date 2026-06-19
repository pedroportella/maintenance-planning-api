# Security And Operations

Implemented local review controls:

- safe problem-details errors;
- correlation ids and structured logs;
- separate startup, liveness and readiness health checks;
- migration readiness reporting without automatic startup migrations;
- graceful shutdown state for traffic eligibility;
- synthetic bearer-token authentication for local review;
- role/scope authorization policies for planner, imports and operations routes;
- command route rate limiting;
- idempotency for non-idempotent commands;
- planner work-order querying with allow-listed filters and sorts;
- operations posture with source freshness, event queue depth, dead-letter count and latest event-ingestion failure code;
- non-root API and worker container runtimes, restricted API container smoke and explicit image identity.

Local synthetic tokens:

- `local-planner-token` for planner backlog, planning runs and package decisions;
- `local-import-token` for source-system-shaped import feeds;
- `local-operations-token` for operations posture and migration readiness;
- `local-reviewer-token` for local reviewer smokes across all policies.

Production-next controls:

- replace local test tokens with issuer and audience validated JWT/OIDC configuration;
- add object-level access checks once tenant or site ownership exists in the model;
- add replay controls and outbound event posture after the inbound event path has deployed smoke evidence.

Do not expose secrets, connection strings, stack traces or raw infrastructure details in public responses or docs.

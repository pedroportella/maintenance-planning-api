# Security And Operations

Implemented local review controls:

- safe problem-details errors;
- correlation ids and structured logs;
- separate startup, liveness and readiness health checks;
- migration readiness reporting without automatic startup migrations;
- graceful shutdown state for traffic eligibility;
- synthetic bearer-token authentication for local review;
- role/scope authorization policies for planner read, planner write, imports and operations routes;
- command route rate limiting;
- idempotent import and queued-event handling for safe retries and duplicate deliveries;
- idempotent planning-run creation for safe command retries;
- planner work-order querying with allow-listed filters and sorts;
- operations posture with source freshness, stale `Received` import counts, event queue depth, dead-letter count, outbox pending/failed counts and latest safe failure codes;
- operations-protected dead-letter replay with audit records;
- transactional outbound event outbox records and EventBridge dispatch code in the worker;
- checked inbound and outbound event-contract documentation;
- non-root API and worker container runtimes, restricted API container smoke and explicit image identity.

Operations posture treats `Received` import audits older than
`MaintenancePlanning:Operations:StaleReceivedImportThresholdMinutes` as stale.
The default is 30 minutes and the value is clamped between 1 minute and 1 day.

Local synthetic tokens:

- `local-planner-read-token` for planner backlog reads only;
- `local-planner-token` for planner backlog reads, planning runs and package decisions;
- `local-import-token` for source-system-shaped import feeds;
- `local-operations-token` for operations posture and migration readiness;
- `local-reviewer-token` for local reviewer smokes across all policies.

Production-next controls:

- replace local test tokens with issuer and audience validated JWT/OIDC configuration;
- add object-level access checks once tenant or site ownership exists in the model;
- revisit anonymous command-rate partitioning before public exposure behind shared proxies; local/review traffic partitions by authenticated subject when present and falls back to remote IP for unauthenticated requests;
- add richer outbound event posture once deployed event smoke evidence exists.

Do not expose secrets, connection strings, stack traces or raw infrastructure details in public responses or docs.

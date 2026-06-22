# Review

**Repository:** `maintenance-planning-api`  
**Review date:** 2026-06-22  
**Reviewer:** Claude Code (claude-sonnet-4-6)  
**Scope:** API correctness, security, docs honesty, evidence gaps, deployment readiness.

Findings are ordered by severity. Each entry includes the file, what matters, and a concrete fix. The review reads actual source files; it does not treat docs as evidence of behaviour.

---

## Critical

### C1 — Dead-letter replay: audit record written AFTER the async operation begins

**File:** `src/MaintenancePlanning.Application/Eventing/DeadLetterReplayService.cs`

The replay client is called before the audit record is committed to the database. If the audit write fails (connection lost, timeout) after the replay has already started, there is an unaudited in-flight operation with no recovery record.

Fail-safe operations must write the audit record and commit it first. Only then should the operation begin. If the operation then fails, the audit record reflects the attempt and recovery is possible.

**Fix:** Restructure to: (1) write and commit the audit record, (2) call `StartReplayAsync`, (3) update the audit record with the final outcome. If step 2 throws, the audit record remains and the operator can inspect it.

---

### C2 — Outbox dispatcher: non-atomic publish and mark-published creates double-dispatch risk

**File:** `src/MaintenancePlanning.Application/Eventing/OutboxDispatcher.cs`

The publish call and the `MarkPublishedAsync` call are sequential but not atomic. If the database write to mark the record as published fails (connection lost, rollback) after EventBridge has already accepted the message, the dispatcher will reload the same record on the next poll and publish it again.

EventBridge itself does not enforce exactly-once semantics. Any downstream consumer that does not de-duplicate on `idempotencyKey` or `eventId` will process the event twice.

**Fix:** The at-least-once delivery pattern is acceptable, but it must be documented and downstream consumers must treat `idempotencyKey` as the de-duplication key. The outbound event contracts (`event-contracts.md`) should state this explicitly. Alternatively, mark the record with a `PublishAttemptedAt` timestamp before publishing, so the dispatcher skips it on retry even if `MarkPublishedAsync` has not yet succeeded.

---

## High

### H1 — Planning run creation has no idempotency protection

**File:** `src/MaintenancePlanning.Application/Planning/PlanningService.cs`

Unlike imports — which use a client-supplied `idempotencyKey` and detect same-key/different-body conflicts — `CreatePlanningRunAsync` generates a new `RunNumber` from the current timestamp and a new GUID on every call. A client retry or network duplicate creates a second planning run with a different ID, different recommendations, and a separate outbox record.

**Fix:** Add a client-supplied `idempotencyKey` field to `CreatePlanningRunRequest`. Look up an existing run by that key before creating a new one. Return the existing run ID when a duplicate key is detected. The test coverage for this path is currently absent; add it alongside the fix.

---

### H2 — Planner authorization policy conflates read and write scopes

**File:** `src/MaintenancePlanning.Api/Security/ApiAuthorization.cs`

The planner policy allows access if the user has either `planning:read` OR `planning:write` scope. A token with only `planning:read` can therefore call `POST /api/v1/planning-runs` and `POST /api/v1/packages/{id}/decisions`, which are state-mutating commands.

The local `local-planner-token` grants both scopes, so this is not visible in normal local testing. The security tests check that missing tokens return 401 and wrong roles return 403, but do not test that a read-only token is blocked from write endpoints.

**Fix:** Introduce a dedicated write policy requiring `planning:write` scope (or the planner role). Apply it to `POST /api/v1/planning-runs` and `POST /api/v1/packages/{id}/decisions`. Add tests that confirm a `planning:read`-only token receives 403 on those routes.

---

### H3 — Missing database indexes on high-frequency query paths

**File:** `src/MaintenancePlanning.Infrastructure/Persistence/MaintenancePlanningDbContext.cs`

Two query patterns lack covering indexes:

1. `FindLatestFailedImportAsync` queries `integration_imports` by `Status` and orders by `CreatedAtUtc`. Without a `(Status, CreatedAtUtc)` index this scans the table on every operations-posture request.
2. Package decision queries filter `work_order_packages` by `PlanningRunId` and `Status`. Without a `(PlanningRunId, Status)` index this scans all packages for large planning runs.

**Fix:** Add both indexes in a new EF Core migration. Use the fluent API in `OnModelCreating` to match the existing indexing style.

---

### H4 — Domain entities carry no validation invariants

**File:** `src/MaintenancePlanning.Domain/` (all entity files)

All entity properties use auto-property defaults (`= ""`). There are no constructor guards, factory methods, or property-setter validation. It is possible to construct and persist an `Asset` with an empty `SourceSystem` or a `WorkOrder` with an empty `WorkOrderNumber`. Validation currently lives entirely in the application service layer, which means it is bypassed if an entity is constructed directly in tests or migrations.

**Fix:** Either add required-parameter constructors to entities that enforce non-empty strings for business key fields, or add EF Core value-object validation (`.IsRequired().HasMaxLength(...)`) to every business key column. The infrastructure-layer approach requires no domain changes and is the lower-risk path at this stage.

---

## Medium

### M1 — Smoke tests do not exercise any protected API routes

**File:** `scripts/container-smoke.mjs`, `scripts/reviewer-evidence-smoke.mjs`

`container-smoke.mjs` calls only the three public health endpoints (`/health/startup`, `/health/live`, `/health/ready`). `reviewer-evidence-smoke.mjs` checks that documentation files are present on disk. Neither script calls a protected `/api/v1` route with a synthetic token.

This means that a misconfigured auth handler, a broken DI registration for an application service, or a missing middleware would not be caught by the smoke suite. The container is considered healthy as long as the health checks pass.

**Fix:** Add a smoke step that calls at least one read endpoint under each policy:
- `GET /api/v1/operations/migration-readiness` with `local-operations-token`
- `GET /api/v1/work-orders` with `local-planner-token`
- `POST /api/v1/imports/source-work-orders` (empty body) with `local-import-token` — expects 422, not 401/403

These calls do not require a database; they fail gracefully with 503 when persistence is unavailable, which is still distinguishable from 401/403.

---

### M2 — Outbound event payload is not validated before dispatch

**File:** `src/MaintenancePlanning.Application/Eventing/OutboxDispatcher.cs`, `src/MaintenancePlanning.Domain/Planning/OutboxEvent.cs`

`OutboxEvent.PayloadJson` is a plain string stored and dispatched without JSON validation. If serialization produces invalid JSON (null reference, encoding edge case), the invalid payload is published to EventBridge. EventBridge accepts any string body; the failure only surfaces when a downstream consumer tries to parse it.

**Fix:** In `OutboxDispatcher`, attempt to parse `PayloadJson` with `JsonDocument.Parse` before calling `PublishAsync`. If parsing fails, mark the outbox record as permanently failed (not retried) and log the error with the outbox record ID. This keeps corrupted records out of the event bus without silently dropping them.

---

### M3 — EventBridge-wrapped versus raw SQS message handling is undocumented

**File:** `src/MaintenancePlanning.Application/Eventing/EventIngestionService.cs`

The ingestion service silently handles two envelope formats: EventBridge messages (where the event envelope is in `detail`) and raw SQS messages (where the root is the envelope). The dual-mode logic is present but not described in `event-contracts.md` or the worker code comments. A deployment that routes raw SQS messages when EventBridge wrapping is expected (or vice versa) will process events silently in the wrong mode.

**Fix:** Add a section to `docs/event-contracts.md` that specifies exactly which formats the worker accepts, which environment variable (if any) controls the mode, and what the failure behaviour is when the format does not match expectations.

---

### M4 — `local-reviewer-token` grants all roles and all scopes, weakening auth tests

**File:** `src/MaintenancePlanning.Api/Security/TestTokenAuthenticationHandler.cs`

The reviewer token grants the planner, imports, and operations roles plus all four scopes simultaneously. Tests that use the reviewer token as a convenience token will pass authorization checks regardless of whether the individual token types are correctly restricted. The current security tests check that missing tokens return 401 and that wrong roles return 403, but do not verify that the reviewer token is the only token that can cross policy boundaries.

**Fix:** Add tests that use `local-planner-token` to call operations routes (expect 403), and `local-operations-token` to call import routes (expect 403). These tests would have caught the read/write scope conflation in H2.

---

### M5 — Import partial-failure state: `Received` status records are not retried or surfaced

**File:** `src/MaintenancePlanning.Application/Imports/ImportService.cs`

Import records are created with a `Received` status at the start of processing. If the application crashes or the database connection is lost after the import record is inserted but before processing completes, the record remains in `Received` state indefinitely. There is no reaper, no alert on stale `Received` records, and the operations posture endpoint does not surface them.

This is a production-next concern rather than a blocking issue, but it is worth noting so it is not overlooked in a future production hardening pass.

**Fix (production-next):** Add a staleness check to the operations posture endpoint: surface any `Received` imports older than a configurable threshold. This gives operators visibility without requiring a reaper process.

---

## Low / Observations

### L1 — Rate-limit partition falls back to IP address for unauthenticated traffic

**File:** `src/MaintenancePlanning.Api/ApiApplication.cs`

Unauthenticated requests are rate-limited by remote IP address. All `/api/v1` routes require authentication, so this only affects the public health and OpenAPI routes. However, if those routes are called from a shared corporate proxy, all callers share a single rate-limit bucket.

This is low risk given the current prototype scope but is worth revisiting before any public exposure.

---

### L2 — Program.cs delegates all setup without a single entry-point comment

**File:** `src/MaintenancePlanning.Api/Program.cs`

`Program.cs` is a one-liner (`ApiApplication.Create(args).Run()`). The delegation is correct and the pattern is testable, but it leaves no breadcrumb for a reader starting at the entry point. A single line comment pointing to `ApiApplication.cs` would help.

---

### L3 — Dead-letter replay idempotency semantics are not documented

**File:** `src/MaintenancePlanning.Application/Eventing/DeadLetterReplayService.cs`, `docs/api.md`

The replay endpoint has idempotency handling (same reason code + requester + rate produces a hash), but the behaviour of same-day vs different-day replays, or multiple replays per outage, is not described in `docs/api.md`. A reviewer cannot confirm whether a second replay in the same window is intended to be idempotent, rejected, or queued.

**Fix:** Add a brief paragraph to `docs/api.md` under the dead-letter replay section describing the idempotency window and the expected behaviour when two replays with the same parameters arrive in close succession.

---

## What Is Strong

**Import idempotency is enterprise-grade.** Same key + same body replays the stored result atomically. Same key + different body returns 409. Duplicate event IDs within a batch are rejected with an `ignored-duplicate` count. The database enforces this with a unique index on `(SourceSystem, IdempotencyKey)`. Tests cover both duplicate-request scenarios.

**Transactional consistency in imports.** All records for a single import (the import record, assets, work orders, events, functional locations) are written in a single `SaveChangesAsync` call. Either everything persists or nothing does. This is correct and important.

**Problem Details (RFC 7807) used consistently across all endpoints.** Every error response has `type`, `title`, `status`, and a correlation ID extension. This makes error handling deterministic for callers.

**Correlation ID tracking is end-to-end.** Every request gets a correlation ID that flows through middleware, structured logs, and all error responses. This is production-grade observability scaffolding.

**Container smoke uses strong runtime security flags.** The smoke runs the API with `--read-only`, `--cap-drop=ALL`, `--security-opt no-new-privileges`, and hard memory/CPU limits. This is a meaningful check against common container hardening requirements.

**Database indexing matches query patterns.** Outbox polling uses a `(Status, AvailableAtUtc)` compound index. Work order readiness filtering has an index. Import idempotency key lookup is covered by a unique index. These are correct choices.

**Docs are honest about evidence status.** `docs/reviewer-runbook.md` states explicitly: *"No live AWS deployment, simulator publish, worker consumption, SQL projection, dead-letter replay or outbound EventBridge smoke has been run from this repository state."* The evidence status tables in `solution-handover.md` and `solution-architecture.md` distinguish proven from pending without overclaiming.

**Clean Architecture layering is respected.** Domain has zero dependencies. Application depends only on domain interfaces. Infrastructure is never imported by domain or application. This makes the codebase testable and the boundaries auditable.

---

## Most Important Next Evidence To Capture

These are ordered by the credibility they add to the review:

1. **Run `npm run container:smoke` against the built API image.** Currently the smoke only checks health endpoints. Extend it to exercise one protected route under each policy (see M1) and capture the output as evidence. This is fast, cheap, and closes a real gap.

2. **Run `npm run db:smoke` with the local SQL Server and capture a successful run log.** The database smoke exercises migration readiness and the `/api/v1/operations/migration-readiness` endpoint. This proves the EF Core migration path end-to-end without AWS.

3. **Add a concurrent planning-run test.** Send two `POST /api/v1/planning-runs` requests simultaneously (or in rapid succession) and confirm that exactly one planning run is created, or that the second returns the first's ID (once H1 is addressed). Without this, the idempotency claim for planning runs cannot be made.

4. **Capture live AWS evidence in the sequence defined in `docs/reviewer-runbook.md`.** The minimum credible path is: build and push images with digests → Terraform plan reviewed → migration task run → API health and OpenAPI verified through ALB → simulator publishes to EventBridge → worker consumes from SQS into SQL Server → operations posture shows correct queue depth and freshness.

5. **Run the read-only scope authorization test (H2 fix).** Once the planner policy is split into read and write variants, add a test that a `planning:read` token receives 403 on `POST /api/v1/planning-runs`. Capture this as a CI test result.

---

## Wording To Soften Or Tighten

The following phrases appear in docs and README. They are either slightly overclaiming or worth tightening for accuracy:

| Location | Current wording | Suggested change |
| --- | --- | --- |
| `docs/security-and-operations.md` | "checked inbound and outbound event-contract documentation" | "event-contract documentation exists and is structurally validated by `event-contract-smoke.mjs`; schema conformance of live messages is not yet verified" |
| `docs/containerisation.md` | "Restricted Smoke" section implies the read-only run proves the app respects the restriction | Add: "The smoke confirms the container starts under these flags; it does not exhaustively exercise file-write paths that would fail under `--read-only`." |
| `docs/solution-handover.md` (Evidence Status table) | "Local Docker API, simulator, SQL Server and backend-mode web path | Passed on 2026-06-21." | Good as written — this is a specific, dated, honest claim. Keep it. |
| `docs/aws-terraform.md` | "EventBridge, SQS, the dead-letter queue, the worker service definition... are provisioned for review" | Good as written — clearly says provisioned, not exercised. Keep it. |
| `docs/reviewer-runbook.md` | "No live AWS deployment... has been run from this repository state." | Good as written. Keep it prominently. |

The overall docs stance is honest. The one consistent risk is the word *"implemented"* applied to async paths (EventBridge, SQS, worker) without a qualifier. Prefer: *"implemented and unit-tested; not yet exercised from a live AWS stack."*

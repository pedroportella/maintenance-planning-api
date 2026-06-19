# API

HTTP surface:

- `GET /health/startup`
- `GET /health/live`
- `GET /health/ready`
- `GET /openapi/v1.json`
- `GET /api/v1/operations/migration-readiness`
- `POST /api/v1/imports/source-work-orders`
- `POST /api/v1/imports/maintenance-events`
- `GET /api/v1/operations/posture`
- `GET /api/v1/work-orders`
- `GET /api/v1/work-orders/{id}`
- `POST /api/v1/planning-runs`
- `GET /api/v1/planning-runs/{id}`
- `GET /api/v1/planning-runs/{id}/recommendations`
- `POST /api/v1/packages/{id}/decisions`

Errors should use `application/problem+json` with a correlation identifier.

`GET /api/v1/operations/migration-readiness` reports whether SQL Server is configured, reachable and up to date with EF Core migrations. It does not apply migrations.

`GET /api/v1/operations/posture` reports whether import persistence is configured and, when available, the latest import freshness summary. It also includes a synthetic eventing posture placeholder for queue and dead-letter counts until the eventing stage is implemented.

## Authentication

Health and OpenAPI routes are public for local readiness checks. API routes under `/api/v1` require a bearer token in local review mode:

```text
Authorization: Bearer local-reviewer-token
```

Local synthetic tokens are:

- `local-planner-token` for planner reads and decisions;
- `local-import-token` for source-system-shaped import feeds;
- `local-operations-token` for operations posture and migration readiness;
- `local-reviewer-token` for reviewer smoke checks across all policies.

These are not production identity credentials. Deployment identity should replace local tokens with JWT/OIDC issuer and audience validation.

## Imports

Import endpoints are local HTTP contracts for synthetic, source-system-shaped review data. They require database persistence to be configured. When persistence is not configured they return `503` with a safe problem response.

`POST /api/v1/imports/source-work-orders` accepts a batch:

```json
{
  "sourceSystem": "synthetic-source",
  "schemaVersion": "1.0",
  "idempotencyKey": "source-work-orders-2026-01-15",
  "sourceWorkOrders": []
}
```

`POST /api/v1/imports/maintenance-events` accepts a versioned event batch:

```json
{
  "sourceSystem": "synthetic-source",
  "schemaVersion": "1.0",
  "batchIdempotencyKey": "baseline-week-2026-01-15",
  "events": []
}
```

Maintenance events use the checked envelope field names `eventId`, `eventType`, `schemaVersion`, `sourceSystem`, `sourceRecordId`, `correlationId`, `occurredAt`, `publishedAt`, `idempotencyKey` and `payload`.

Both import routes return an import result with accepted, rejected, ignored duplicate and ignored stale counts. Reusing the same idempotency key and request body replays the stored result without creating duplicate rows. Reusing the same idempotency key with a different body returns `409`. Invalid request shape returns `422`.

Accepted work-order records retain source identifiers, source-data readiness and issue summaries using `Ready`, `NeedsReview` and `Blocked`.

## Work Orders

`GET /api/v1/work-orders` returns a planner-facing backlog page with readiness and source-data issue summaries. Supported query parameters are allow-listed:

- `cursor`;
- `pageSize`, from 1 to 100;
- `backlog`, defaulting to `true`;
- `priority`;
- `functionalLocation`;
- `readiness`, using `Ready`, `NeedsReview` or `Blocked`;
- `status`, using work-order lifecycle status names;
- `updatedSinceUtc`;
- `updatedBeforeUtc`;
- `sort`, using `dueAtUtc`, `priority`, `requiredStartUtc`, `updatedAtUtc` or `workOrderNumber`, with a leading `-` for descending order.

Example:

```text
GET /api/v1/work-orders?pageSize=25&readiness=Ready&sort=dueAtUtc
```

`GET /api/v1/work-orders/{id}` returns a single work order detail. Both work-order routes require planner scope or role. Unsupported filter values return `422`.

## Planning

Planning endpoints require database persistence to be configured. When persistence is not configured they return `503` with a safe problem response.

`POST /api/v1/planning-runs` creates a planning run over imported synthetic work orders. The route completes deterministic local recommendation generation during the request and returns `202 Accepted` with a `Location` header for the run:

```json
{
  "horizon": "two-week",
  "horizonStartUtc": "2026-01-15T00:00:00Z",
  "horizonEndUtc": "2026-01-29T00:00:00Z",
  "requestedBy": "local-review"
}
```

`GET /api/v1/planning-runs/{id}` returns the run status, horizon and recommendation counts.

`GET /api/v1/planning-runs/{id}/recommendations` returns package recommendations with:

- deterministic score and actionability (`ready-now`, `needs-resolution` or `blocked`);
- source-data readiness summary using `Ready`, `NeedsReview` and `Blocked`;
- blocker summaries grouped as data, parts, crew or window constraints;
- planner-facing explanation text;
- package work-order items and prior planner decisions.

`POST /api/v1/packages/{id}/decisions` records an audited planner decision:

```json
{
  "decision": "Accepted",
  "reasonCode": "ready-for-weekly-plan",
  "notes": "Synthetic planner decision for local review.",
  "decidedBy": "local-review"
}
```

Allowed decision values are `Accepted`, `Rejected` and `Deferred`. A decision updates package status and records one or more decision audit rows. Invalid decision payloads return `422`.

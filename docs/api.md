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
- `POST /api/v1/planning-runs`
- `GET /api/v1/planning-runs/{id}`
- `GET /api/v1/planning-runs/{id}/recommendations`
- `POST /api/v1/packages/{id}/decisions`

Errors should use `application/problem+json` with a correlation identifier.

`GET /api/v1/operations/migration-readiness` reports whether SQL Server is configured, reachable and up to date with EF Core migrations. It does not apply migrations.

`GET /api/v1/operations/posture` reports whether import persistence is configured and, when available, the latest import freshness summary.

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

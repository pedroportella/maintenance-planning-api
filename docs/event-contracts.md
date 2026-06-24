# Event Contracts

Inbound synthetic maintenance events use a versioned envelope:

- `eventId`
- `eventType`
- `schemaVersion`
- `sourceSystem`
- `sourceRecordId`
- `correlationId`
- `occurredAt`
- `publishedAt`
- `idempotencyKey`
- `payload`

Initial event types:

- `WorkOrderCreated`
- `WorkOrderUpdated`
- `WorkOrderStatusChanged`
- `MajorEventWindowPublished`
- `PartsAvailabilityChanged`
- `CrewCapacityChanged`

The HTTP import endpoint accepts these events at `POST /api/v1/imports/maintenance-events` inside a batch with `sourceSystem`, `schemaVersion`, `batchIdempotencyKey` and `events`.

The worker consumes EventBridge-delivered SQS messages. The message body may be either the raw event envelope or an EventBridge event with the envelope in `detail`. EventBridge message ids become batch idempotency keys, while the inner event id and event idempotency key protect SQL projections from duplicate source events.

Work-order event payloads retain source-data readiness as:

```json
{
  "sourceDataReadiness": {
    "status": "Ready",
    "issueCode": null,
    "issueDetail": null,
    "validationIssues": []
  }
}
```

Allowed readiness statuses are `Ready`, `NeedsReview` and `Blocked`.

The import result reports accepted, rejected, ignored duplicate and ignored stale counts. Duplicate event idempotency keys are ignored, stale work-order updates do not replace newer source state, and rejected events remain audit-visible without creating planning records. Malformed queued messages are recorded as failed event-ingestion imports with a failure code and without storing raw payload text in operations responses.

## Outbound Domain Events

The API records outbound planning events in the SQL outbox inside the same transaction as the planning change. The worker dispatches pending outbox rows to EventBridge when `MAINTENANCE_PLANNING_EVENT_BUS_NAME` is configured.

Outbound publishing is an at-least-once delivery path. Downstream consumers should de-duplicate on the outbound `idempotencyKey` and tolerate a retry if EventBridge accepts an event but the worker fails before marking the outbox row as published.

Outbound events use this envelope:

- `eventId`
- `eventType`
- `schemaVersion`
- `sourceSystem`
- `aggregateType`
- `aggregateId`
- `correlationId`
- `occurredAt`
- `recordedAt`
- `idempotencyKey`
- `payload`

Initial outbound event types:

- `planning.run.completed`
- `planning.package.decision-recorded`

The AsyncAPI-style descriptor is [outbound-events.asyncapi.json](outbound-events.asyncapi.json). Checked JSON schemas live in:

- [planning-run-completed.schema.json](../contracts/planning-run-completed.schema.json)
- [package-decision-recorded.schema.json](../contracts/package-decision-recorded.schema.json)

Validate the descriptor and schema references with:

```bash
node scripts/event-contract-smoke.mjs
```

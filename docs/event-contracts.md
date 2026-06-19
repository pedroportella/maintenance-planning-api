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

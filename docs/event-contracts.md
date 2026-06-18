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

The import result reports accepted, rejected, ignored duplicate and ignored stale counts. Duplicate event idempotency keys are ignored, stale work-order updates do not replace newer source state, and rejected events remain audit-visible without creating planning records.

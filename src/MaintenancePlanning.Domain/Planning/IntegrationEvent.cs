namespace MaintenancePlanning.Domain.Planning;

public sealed class IntegrationEvent
{
    public Guid Id { get; set; }

    public Guid IntegrationImportId { get; set; }

    public IntegrationImport IntegrationImport { get; set; } = null!;

    public string EventId { get; set; } = "";

    public string EventType { get; set; } = "";

    public string SchemaVersion { get; set; } = "";

    public string SourceSystem { get; set; } = "";

    public string SourceRecordId { get; set; } = "";

    public string CorrelationId { get; set; } = "";

    public string IdempotencyKey { get; set; } = "";

    public string Disposition { get; set; } = "";

    public IntegrationEventStatus Status { get; set; }

    public string? WorkOrderSourceId { get; set; }

    public string PayloadHash { get; set; } = "";

    public DateTimeOffset OccurredAtUtc { get; set; }

    public DateTimeOffset PublishedAtUtc { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }

    public string? Readiness { get; set; }

    public string? ValidationIssueCode { get; set; }
}

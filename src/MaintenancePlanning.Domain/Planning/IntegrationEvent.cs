namespace MaintenancePlanning.Domain.Planning;

public sealed class IntegrationEvent
{
    public Guid Id { get; set; }

    public Guid IntegrationImportId { get; set; }

    public IntegrationImport IntegrationImport { get; set; } = null!;

    public string EventId { get; set; } = "";

    public string EventType { get; set; } = "";

    public IntegrationEventStatus Status { get; set; }

    public string? WorkOrderSourceId { get; set; }

    public string PayloadHash { get; set; } = "";

    public DateTimeOffset OccurredAtUtc { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }
}

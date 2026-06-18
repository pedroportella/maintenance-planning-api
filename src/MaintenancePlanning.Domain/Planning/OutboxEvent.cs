namespace MaintenancePlanning.Domain.Planning;

public sealed class OutboxEvent
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = "";

    public string AggregateType { get; set; } = "";

    public Guid AggregateId { get; set; }

    public string PayloadJson { get; set; } = "";

    public OutboxEventStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset AvailableAtUtc { get; set; }

    public DateTimeOffset? PublishedAtUtc { get; set; }

    public string? LastErrorCode { get; set; }
}

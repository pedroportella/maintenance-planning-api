namespace MaintenancePlanning.Domain.Planning;

public sealed class IntegrationImport
{
    public Guid Id { get; set; }

    public string SourceSystem { get; set; } = "";

    public string ImportKind { get; set; } = "";

    public string IdempotencyKey { get; set; } = "";

    public string RequestHash { get; set; } = "";

    public IntegrationImportStatus Status { get; set; }

    public int ReceivedCount { get; set; }

    public int AcceptedCount { get; set; }

    public int RejectedCount { get; set; }

    public int IgnoredDuplicateCount { get; set; }

    public int IgnoredStaleCount { get; set; }

    public string? FailureCode { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public ICollection<IntegrationEvent> Events { get; } = new List<IntegrationEvent>();
}

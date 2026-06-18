namespace MaintenancePlanning.Domain.Planning;

public sealed class MajorEvent
{
    public Guid Id { get; set; }

    public Guid? AssetId { get; set; }

    public Asset? Asset { get; set; }

    public Guid? FunctionalLocationId { get; set; }

    public FunctionalLocation? FunctionalLocation { get; set; }

    public string SourceSystem { get; set; } = "";

    public string SourceId { get; set; } = "";

    public string EventType { get; set; } = "";

    public string Title { get; set; } = "";

    public string Severity { get; set; } = "";

    public DateTimeOffset StartsAtUtc { get; set; }

    public DateTimeOffset? EndsAtUtc { get; set; }

    public string? ReadinessIssueCode { get; set; }

    public DateTimeOffset ImportedAtUtc { get; set; }
}

namespace MaintenancePlanning.Domain.Planning;

public sealed class WorkOrder
{
    public Guid Id { get; set; }

    public Guid? AssetId { get; set; }

    public Asset? Asset { get; set; }

    public Guid? FunctionalLocationId { get; set; }

    public FunctionalLocation? FunctionalLocation { get; set; }

    public string SourceSystem { get; set; } = "";

    public string SourceId { get; set; } = "";

    public string WorkOrderNumber { get; set; } = "";

    public string Title { get; set; } = "";

    public string WorkType { get; set; } = "";

    public string Priority { get; set; } = "";

    public WorkOrderLifecycleStatus Status { get; set; }

    public SourceDataReadiness Readiness { get; set; }

    public string? ReadinessIssueCode { get; set; }

    public string? ReadinessIssueDetail { get; set; }

    public DateTimeOffset? RequiredStartUtc { get; set; }

    public DateTimeOffset? DueAtUtc { get; set; }

    public DateTimeOffset? ScheduledStartUtc { get; set; }

    public decimal? EstimatedHours { get; set; }

    public DateTimeOffset SourceUpdatedAtUtc { get; set; }

    public DateTimeOffset ImportedAtUtc { get; set; }

    public string SourcePayloadHash { get; set; } = "";

    public ICollection<PackageItem> PackageItems { get; } = new List<PackageItem>();

    public ICollection<PlannerDecision> PlannerDecisions { get; } = new List<PlannerDecision>();
}

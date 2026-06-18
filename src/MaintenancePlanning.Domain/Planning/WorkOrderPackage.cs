namespace MaintenancePlanning.Domain.Planning;

public sealed class WorkOrderPackage
{
    public Guid Id { get; set; }

    public Guid PlanningRunId { get; set; }

    public PlanningRun PlanningRun { get; set; } = null!;

    public string PackageNumber { get; set; } = "";

    public string Title { get; set; } = "";

    public PackageStatus Status { get; set; }

    public decimal EstimatedHours { get; set; }

    public DateTimeOffset? PlannedStartUtc { get; set; }

    public DateTimeOffset? PlannedEndUtc { get; set; }

    public string RecommendationRationale { get; set; } = "";

    public ICollection<PackageItem> Items { get; } = new List<PackageItem>();

    public ICollection<PlannerDecision> Decisions { get; } = new List<PlannerDecision>();
}

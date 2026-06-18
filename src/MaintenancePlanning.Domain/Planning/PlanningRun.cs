namespace MaintenancePlanning.Domain.Planning;

public sealed class PlanningRun
{
    public Guid Id { get; set; }

    public string RunNumber { get; set; } = "";

    public PlanningRunStatus Status { get; set; }

    public string Horizon { get; set; } = "";

    public DateTimeOffset HorizonStartUtc { get; set; }

    public DateTimeOffset HorizonEndUtc { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string RequestedBy { get; set; } = "";

    public ICollection<WorkOrderPackage> Packages { get; } = new List<WorkOrderPackage>();
}

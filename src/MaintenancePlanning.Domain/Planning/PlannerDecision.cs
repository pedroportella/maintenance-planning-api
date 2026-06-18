namespace MaintenancePlanning.Domain.Planning;

public sealed class PlannerDecision
{
    public Guid Id { get; set; }

    public Guid WorkOrderPackageId { get; set; }

    public WorkOrderPackage WorkOrderPackage { get; set; } = null!;

    public Guid? WorkOrderId { get; set; }

    public WorkOrder? WorkOrder { get; set; }

    public PlannerDecisionType Decision { get; set; }

    public string ReasonCode { get; set; } = "";

    public string? Notes { get; set; }

    public DateTimeOffset DecidedAtUtc { get; set; }

    public string DecidedBy { get; set; } = "";
}

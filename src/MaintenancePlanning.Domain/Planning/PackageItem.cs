namespace MaintenancePlanning.Domain.Planning;

public sealed class PackageItem
{
    public Guid Id { get; set; }

    public Guid WorkOrderPackageId { get; set; }

    public WorkOrderPackage WorkOrderPackage { get; set; } = null!;

    public Guid WorkOrderId { get; set; }

    public WorkOrder WorkOrder { get; set; } = null!;

    public int Sequence { get; set; }

    public string FitReason { get; set; } = "";
}

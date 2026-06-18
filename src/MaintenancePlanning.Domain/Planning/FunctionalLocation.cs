namespace MaintenancePlanning.Domain.Planning;

public sealed class FunctionalLocation
{
    public Guid Id { get; set; }

    public string SourceSystem { get; set; } = "";

    public string SourceId { get; set; } = "";

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public string? ParentSourceId { get; set; }

    public DateTimeOffset SourceUpdatedAtUtc { get; set; }

    public DateTimeOffset ImportedAtUtc { get; set; }

    public ICollection<Asset> Assets { get; } = new List<Asset>();

    public ICollection<WorkOrder> WorkOrders { get; } = new List<WorkOrder>();

    public ICollection<MajorEvent> MajorEvents { get; } = new List<MajorEvent>();
}

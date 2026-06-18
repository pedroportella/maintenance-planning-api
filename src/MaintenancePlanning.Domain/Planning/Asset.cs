namespace MaintenancePlanning.Domain.Planning;

public sealed class Asset
{
    public Guid Id { get; set; }

    public Guid FunctionalLocationId { get; set; }

    public FunctionalLocation FunctionalLocation { get; set; } = null!;

    public string SourceSystem { get; set; } = "";

    public string SourceId { get; set; } = "";

    public string AssetNumber { get; set; } = "";

    public string Name { get; set; } = "";

    public string Criticality { get; set; } = "";

    public DateTimeOffset SourceUpdatedAtUtc { get; set; }

    public DateTimeOffset ImportedAtUtc { get; set; }

    public ICollection<WorkOrder> WorkOrders { get; } = new List<WorkOrder>();

    public ICollection<MajorEvent> MajorEvents { get; } = new List<MajorEvent>();
}

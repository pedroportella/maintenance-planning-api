namespace MaintenancePlanning.Application.Readiness;

public sealed record ReadinessSnapshot(bool IsReady, IReadOnlyCollection<ReadinessDependency> Dependencies)
{
    public static ReadinessSnapshot Ready() => new(true, Array.Empty<ReadinessDependency>());

    public static ReadinessSnapshot NotReady(params ReadinessDependency[] dependencies) => new(false, dependencies);
}

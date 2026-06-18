using MaintenancePlanning.Application.Readiness;

namespace MaintenancePlanning.Infrastructure.Readiness;

internal sealed class NoExternalDependencyReadinessProbe : IReadinessProbe
{
    public Task<ReadinessSnapshot> CheckAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ReadinessSnapshot.Ready());
    }
}

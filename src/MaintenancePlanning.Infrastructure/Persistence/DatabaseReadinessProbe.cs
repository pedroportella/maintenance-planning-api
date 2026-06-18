using MaintenancePlanning.Application.Persistence;
using MaintenancePlanning.Application.Readiness;
using Microsoft.Extensions.DependencyInjection;

namespace MaintenancePlanning.Infrastructure.Persistence;

internal sealed class DatabaseReadinessProbe(IServiceScopeFactory scopeFactory) : IReadinessProbe
{
    public async Task<ReadinessSnapshot> CheckAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var reporter = scope.ServiceProvider.GetRequiredService<IMigrationReadinessReporter>();
        var report = await reporter.CheckAsync(cancellationToken);

        return report.IsReady
            ? ReadinessSnapshot.Ready()
            : ReadinessSnapshot.NotReady(new ReadinessDependency("database", false));
    }
}

using MaintenancePlanning.Api.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MaintenancePlanning.Api.Health;

public sealed class StartupHealthCheck(ApplicationLifecycleState lifecycleState) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = lifecycleState.StartupCompleted
            ? HealthCheckResult.Healthy("Startup has completed.")
            : HealthCheckResult.Unhealthy("Startup is still in progress.");

        return Task.FromResult(result);
    }
}

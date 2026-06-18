using MaintenancePlanning.Api.Hosting;
using MaintenancePlanning.Application.Readiness;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MaintenancePlanning.Api.Health;

public sealed class ReadinessHealthCheck(
    ApplicationLifecycleState lifecycleState,
    IReadinessProbe readinessProbe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (lifecycleState.IsStopping)
        {
            return HealthCheckResult.Unhealthy("Service is stopping.");
        }

        if (!lifecycleState.StartupCompleted)
        {
            return HealthCheckResult.Unhealthy("Startup has not completed.");
        }

        var snapshot = await readinessProbe.CheckAsync(cancellationToken);

        return snapshot.IsReady
            ? HealthCheckResult.Healthy("Service is ready.")
            : HealthCheckResult.Unhealthy("Required dependencies are not ready.");
    }
}

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MaintenancePlanning.Api.Health;

public sealed class LivenessHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("Process is live."));
    }
}

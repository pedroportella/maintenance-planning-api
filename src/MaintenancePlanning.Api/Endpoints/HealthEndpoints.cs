using MaintenancePlanning.Api.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MaintenancePlanning.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var health = endpoints.MapGroup("/health").WithTags("Health");

        health
            .MapGet("/startup", (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
                CheckHealthAsync(healthChecks, HealthCheckTags.Startup, cancellationToken))
            .WithName("GetStartupHealth")
            .WithSummary("Get startup health")
            .WithDescription("Public probe for startup completion. Returns unhealthy until hosted services have completed startup.")
            .Produces<HealthEndpointResponse>(StatusCodes.Status200OK)
            .Produces<HealthEndpointResponse>(StatusCodes.Status503ServiceUnavailable);

        health
            .MapGet("/live", (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
                CheckHealthAsync(healthChecks, HealthCheckTags.Live, cancellationToken))
            .WithName("GetLivenessHealth")
            .WithSummary("Get liveness health")
            .WithDescription("Public probe that reports whether the API process is running and not shutting down.")
            .Produces<HealthEndpointResponse>(StatusCodes.Status200OK)
            .Produces<HealthEndpointResponse>(StatusCodes.Status503ServiceUnavailable);

        health
            .MapGet("/ready", (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
                CheckHealthAsync(healthChecks, HealthCheckTags.Ready, cancellationToken))
            .WithName("GetReadinessHealth")
            .WithSummary("Get readiness health")
            .WithDescription("Public probe for dependencies required to handle local review traffic.")
            .Produces<HealthEndpointResponse>(StatusCodes.Status200OK)
            .Produces<HealthEndpointResponse>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> CheckHealthAsync(
        HealthCheckService healthChecks,
        string tag,
        CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains(tag),
            cancellationToken);

        return new HealthReportHttpResult(report);
    }
}

using System.Text.Json;
using MaintenancePlanning.Api.Middleware;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MaintenancePlanning.Api.Health;

public sealed class HealthReportHttpResult(HealthReport report) : IResult
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var correlationIdAccessor = httpContext.RequestServices.GetRequiredService<ICorrelationIdAccessor>();
        var correlationId = correlationIdAccessor.CorrelationId ?? httpContext.TraceIdentifier;
        var response = new HealthEndpointResponse(
            report.Status.ToString(),
            correlationId,
            report.Entries
                .Select(entry => new HealthEndpointCheckResponse(entry.Key, entry.Value.Status.ToString()))
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToArray());

        httpContext.Response.StatusCode = report.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            response,
            JsonOptions,
            httpContext.RequestAborted);
    }
}

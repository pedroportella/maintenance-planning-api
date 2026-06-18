using MaintenancePlanning.Application.Persistence;

namespace MaintenancePlanning.Api.Endpoints;

public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var operations = endpoints.MapGroup("/api/v1/operations").WithTags("Operations");

        operations
            .MapGet("/migration-readiness", CheckMigrationReadinessAsync)
            .WithName("GetMigrationReadiness")
            .Produces<MigrationReadinessReport>(StatusCodes.Status200OK)
            .Produces<MigrationReadinessReport>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> CheckMigrationReadinessAsync(
        IMigrationReadinessReporter reporter,
        CancellationToken cancellationToken)
    {
        var report = await reporter.CheckAsync(cancellationToken);

        return report.IsReady
            ? Results.Ok(report)
            : Results.Json(report, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

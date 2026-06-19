using MaintenancePlanning.Api.Security;
using MaintenancePlanning.Application.Imports;
using MaintenancePlanning.Application.Operations;
using MaintenancePlanning.Application.Persistence;

namespace MaintenancePlanning.Api.Endpoints;

public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var operations = endpoints
            .MapGroup("/api/v1/operations")
            .WithTags("Operations")
            .RequireAuthorization(ApiAuthorization.OperationsPolicy);

        operations
            .MapGet("/migration-readiness", CheckMigrationReadinessAsync)
            .WithName("GetMigrationReadiness")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces<MigrationReadinessReport>(StatusCodes.Status200OK)
            .Produces<MigrationReadinessReport>(StatusCodes.Status503ServiceUnavailable);

        operations
            .MapGet("/posture", GetPostureAsync)
            .WithName("GetOperationsPosture")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces<OperationsPostureReport>(StatusCodes.Status200OK);

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

    private static async Task<IResult> GetPostureAsync(
        IImportStore importStore,
        CancellationToken cancellationToken)
    {
        var latestImport = importStore.IsConfigured
            ? await importStore.FindLatestImportAsync(cancellationToken)
            : null;

        return Results.Ok(new OperationsPostureReport(
            DatabaseConfigured: importStore.IsConfigured,
            Status: importStore.IsConfigured ? "healthy" : "degraded",
            IssueCode: importStore.IsConfigured ? null : "import-persistence-not-configured",
            LatestImport: latestImport is null
                ? null
                : new LatestImportFreshness(
                    latestImport.ImportId,
                    latestImport.SourceSystem,
                    latestImport.ImportKind,
                    latestImport.Status,
                    latestImport.ReceivedCount,
                    latestImport.AcceptedCount,
                    latestImport.RejectedCount,
                    latestImport.IgnoredDuplicateCount,
                    latestImport.IgnoredStaleCount,
                    latestImport.ReceivedAtUtc,
                    latestImport.CompletedAtUtc),
            Eventing: new IntegrationEventingPosture(
                PublishMode: "not-configured",
                QueueDepth: 0,
                DeadLetterCount: 0,
                LastFailureCode: null),
            CheckedAtUtc: DateTimeOffset.UtcNow));
    }
}

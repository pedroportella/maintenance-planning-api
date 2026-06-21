using MaintenancePlanning.Api.Security;
using MaintenancePlanning.Application.Eventing;
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

        operations
            .MapPost("/eventing/dead-letter-replays", StartDeadLetterReplayAsync)
            .WithName("StartDeadLetterReplay")
            .RequireRateLimiting(ApiRateLimitPolicies.Command)
            .Accepts<StartDeadLetterReplayRequest>("application/json")
            .Produces<DeadLetterReplayResult>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

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
        IEventingPostureReporter eventingPostureReporter,
        CancellationToken cancellationToken)
    {
        var latestImport = importStore.IsConfigured
            ? await importStore.FindLatestImportAsync(cancellationToken)
            : null;
        var latestFailedImport = importStore.IsConfigured
            ? await importStore.FindLatestFailedImportAsync(cancellationToken)
            : null;
        var eventingPosture = await eventingPostureReporter.CheckAsync(cancellationToken);

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
                    latestImport.IdempotencyKey,
                    latestImport.Status,
                    latestImport.ReceivedCount,
                    latestImport.AcceptedCount,
                    latestImport.RejectedCount,
                    latestImport.IgnoredDuplicateCount,
                    latestImport.IgnoredStaleCount,
                    latestImport.ReceivedAtUtc,
                    latestImport.CompletedAtUtc),
            Eventing: eventingPosture with
            {
                LastFailureCode = latestFailedImport?.FailureCode ?? eventingPosture.LastFailureCode
            },
            CheckedAtUtc: DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> StartDeadLetterReplayAsync(
        StartDeadLetterReplayRequest request,
        IDeadLetterReplayService replayService,
        CancellationToken cancellationToken)
    {
        var outcome = await replayService.StartReplayAsync(request, cancellationToken);

        return outcome.Result is not null
            ? Results.Accepted($"/api/v1/operations/eventing/dead-letter-replays/{outcome.Result.AuditId}", outcome.Result)
            : ToReplayProblemResult(outcome.Problem);
    }

    private static IResult ToReplayProblemResult(DeadLetterReplayProblem? problem)
    {
        problem ??= new DeadLetterReplayProblem(
            StatusCodes.Status500InternalServerError,
            "Dead-letter replay failed.",
            "The dead-letter replay request could not be processed.",
            "dead-letter-replay-failed");

        if (problem.Errors is not null)
        {
            return Results.ValidationProblem(
                problem.Errors.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
                title: problem.Title,
                detail: problem.Detail,
                statusCode: problem.StatusCode,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = problem.Code
                });
        }

        return Results.Problem(
            title: problem.Title,
            detail: problem.Detail,
            statusCode: problem.StatusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = problem.Code
            });
    }
}

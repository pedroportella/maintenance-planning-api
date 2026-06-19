using MaintenancePlanning.Api.Security;
using MaintenancePlanning.Application.Planning;

namespace MaintenancePlanning.Api.Endpoints;

public static class PlanningEndpoints
{
    public static IEndpointRouteBuilder MapPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var planningRuns = endpoints
            .MapGroup("/api/v1/planning-runs")
            .WithTags("Planning")
            .RequireAuthorization(ApiAuthorization.PlannerPolicy);

        planningRuns
            .MapPost("", CreatePlanningRunAsync)
            .WithName("CreatePlanningRun")
            .RequireRateLimiting(ApiRateLimitPolicies.Command)
            .Accepts<CreatePlanningRunRequest>("application/json")
            .Produces<PlanningRunResult>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        planningRuns
            .MapGet("/{id:guid}", GetPlanningRunAsync)
            .WithName("GetPlanningRun")
            .Produces<PlanningRunResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        planningRuns
            .MapGet("/{id:guid}/recommendations", GetRecommendationsAsync)
            .WithName("GetPlanningRunRecommendations")
            .Produces<PlanningRecommendationsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints
            .MapPost("/api/v1/packages/{id:guid}/decisions", RecordPackageDecisionAsync)
            .WithName("RecordPackageDecision")
            .WithTags("Planning")
            .RequireAuthorization(ApiAuthorization.PlannerPolicy)
            .RequireRateLimiting(ApiRateLimitPolicies.Command)
            .Accepts<RecordPackageDecisionRequest>("application/json")
            .Produces<PackageDecisionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> CreatePlanningRunAsync(
        CreatePlanningRunRequest request,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var outcome = await planningService.CreatePlanningRunAsync(request, cancellationToken);

        if (outcome.Result is not null)
        {
            return Results.Accepted($"/api/v1/planning-runs/{outcome.Result.Id}", outcome.Result);
        }

        return ToProblemResult(outcome.Problem);
    }

    private static async Task<IResult> GetPlanningRunAsync(
        Guid id,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var outcome = await planningService.GetPlanningRunAsync(id, cancellationToken);

        return ToResult(outcome);
    }

    private static async Task<IResult> GetRecommendationsAsync(
        Guid id,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var outcome = await planningService.GetRecommendationsAsync(id, cancellationToken);

        return ToResult(outcome);
    }

    private static async Task<IResult> RecordPackageDecisionAsync(
        Guid id,
        RecordPackageDecisionRequest request,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var outcome = await planningService.RecordPackageDecisionAsync(id, request, cancellationToken);

        return ToResult(outcome);
    }

    private static IResult ToResult<T>(PlanningProcessingOutcome<T> outcome)
    {
        return outcome.Result is not null ? Results.Ok(outcome.Result) : ToProblemResult(outcome.Problem);
    }

    private static IResult ToProblemResult(PlanningProblem? problem)
    {
        problem ??= new PlanningProblem(
            StatusCodes.Status500InternalServerError,
            "Planning request failed.",
            "The planning request could not be processed.",
            "planning-request-failed");

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

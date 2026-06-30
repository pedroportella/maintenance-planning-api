using MaintenancePlanning.Api.Security;
using MaintenancePlanning.Application.Planning;

namespace MaintenancePlanning.Api.Endpoints;

public static class WorkOrderEndpoints
{
    public static IEndpointRouteBuilder MapWorkOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var workOrders = endpoints
            .MapGroup("/api/v1/work-orders")
            .WithTags("Work Orders")
            .RequireAuthorization(ApiAuthorization.PlannerReadPolicy);

        workOrders
            .MapGet("", QueryWorkOrdersAsync)
            .WithName("ListWorkOrders")
            .Produces<WorkOrderQueryResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        workOrders
            .MapGet("/{id:guid}", GetWorkOrderAsync)
            .WithName("GetWorkOrder")
            .Produces<WorkOrderDetailResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> QueryWorkOrdersAsync(
        [AsParameters] WorkOrderQueryParameters parameters,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var outcome = await planningService.QueryWorkOrdersAsync(parameters.ToRequest(), cancellationToken);

        return ToResult(outcome);
    }

    private static async Task<IResult> GetWorkOrderAsync(
        Guid id,
        IPlanningService planningService,
        CancellationToken cancellationToken)
    {
        var outcome = await planningService.GetWorkOrderAsync(id, cancellationToken);

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

    private sealed class WorkOrderQueryParameters
    {
        public string? Cursor { get; init; }

        public int? PageSize { get; init; }

        public bool? Backlog { get; init; }

        public string? Priority { get; init; }

        public string? FunctionalLocation { get; init; }

        public string? Readiness { get; init; }

        public string? Status { get; init; }

        public DateTimeOffset? UpdatedSinceUtc { get; init; }

        public DateTimeOffset? UpdatedBeforeUtc { get; init; }

        public string? Sort { get; init; }

        public WorkOrderQueryRequest ToRequest()
        {
            return new WorkOrderQueryRequest
            {
                Cursor = Cursor,
                PageSize = PageSize,
                Backlog = Backlog,
                Priority = Priority,
                FunctionalLocation = FunctionalLocation,
                Readiness = Readiness,
                Status = Status,
                UpdatedSinceUtc = UpdatedSinceUtc,
                UpdatedBeforeUtc = UpdatedBeforeUtc,
                Sort = Sort
            };
        }
    }
}

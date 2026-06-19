using MaintenancePlanning.Application.Imports;
using MaintenancePlanning.Api.Security;

namespace MaintenancePlanning.Api.Endpoints;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var imports = endpoints
            .MapGroup("/api/v1/imports")
            .WithTags("Imports")
            .RequireAuthorization(ApiAuthorization.ImportsPolicy);

        imports
            .MapPost("/source-work-orders", ImportSourceWorkOrdersAsync)
            .WithName("ImportSourceWorkOrders")
            .RequireRateLimiting(ApiRateLimitPolicies.Command)
            .Accepts<SourceWorkOrderImportRequest>("application/json")
            .Produces<ImportResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        imports
            .MapPost("/maintenance-events", ImportMaintenanceEventsAsync)
            .WithName("ImportMaintenanceEvents")
            .RequireRateLimiting(ApiRateLimitPolicies.Command)
            .Accepts<MaintenanceEventImportRequest>("application/json")
            .Produces<ImportResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> ImportSourceWorkOrdersAsync(
        SourceWorkOrderImportRequest request,
        IImportService importService,
        CancellationToken cancellationToken)
    {
        var outcome = await importService.ImportSourceWorkOrdersAsync(request, cancellationToken);

        return ToResult(outcome);
    }

    private static async Task<IResult> ImportMaintenanceEventsAsync(
        MaintenanceEventImportRequest request,
        IImportService importService,
        CancellationToken cancellationToken)
    {
        var outcome = await importService.ImportMaintenanceEventsAsync(request, cancellationToken);

        return ToResult(outcome);
    }

    private static IResult ToResult(ImportProcessingOutcome outcome)
    {
        if (outcome.Result is not null)
        {
            return Results.Ok(outcome.Result);
        }

        var problem = outcome.Problem ?? new ImportProblem(
            StatusCodes.Status500InternalServerError,
            "Import failed.",
            "The import request could not be processed.",
            "import-failed");

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

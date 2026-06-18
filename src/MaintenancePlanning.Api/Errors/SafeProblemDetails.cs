using MaintenancePlanning.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MaintenancePlanning.Api.Errors;

public static class SafeProblemDetails
{
    private const string UnexpectedErrorTitle = "Unexpected service error.";

    public static void Configure(ProblemDetailsOptions options)
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Detail = null;

            if (context.ProblemDetails.Status >= StatusCodes.Status500InternalServerError)
            {
                context.ProblemDetails.Title = UnexpectedErrorTitle;
            }

            var correlationIdAccessor = context.HttpContext.RequestServices.GetRequiredService<ICorrelationIdAccessor>();
            var correlationId = correlationIdAccessor.CorrelationId
                ?? GetCorrelationIdFromRequest(context.HttpContext)
                ?? context.HttpContext.TraceIdentifier;

            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        };
    }

    public static async Task WriteUnhandledExceptionAsync(HttpContext context)
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is not null)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("MaintenancePlanning.Api.Errors");
            logger.LogError(exception, "Unhandled request exception.");
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = UnexpectedErrorTitle,
            Type = "https://httpstatuses.com/500",
            Instance = context.Request.Path
        };

        await problemDetailsService.WriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problemDetails
            });
    }

    private static string? GetCorrelationIdFromRequest(HttpContext context)
    {
        return context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
            ? value as string
            : null;
    }
}

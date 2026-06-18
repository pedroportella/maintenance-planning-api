using MaintenancePlanning.Api.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace MaintenancePlanning.Api.Middleware;

public sealed class GracefulShutdownMiddleware(
    RequestDelegate next,
    ApplicationLifecycleState lifecycleState)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (lifecycleState.IsStopping && !IsHealthRequest(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.WriteAsync(
                new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status503ServiceUnavailable,
                        Title = "Service is stopping.",
                        Type = "https://httpstatuses.com/503",
                        Instance = context.Request.Path
                    }
                });
            return;
        }

        await next(context);
    }

    private static bool IsHealthRequest(PathString path)
    {
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
    }
}

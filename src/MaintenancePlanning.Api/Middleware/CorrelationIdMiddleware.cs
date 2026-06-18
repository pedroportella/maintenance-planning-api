using System.Collections.ObjectModel;

namespace MaintenancePlanning.Api.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    private const int MaxCorrelationIdLength = 128;

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationIdAccessor)
    {
        var correlationId = ResolveCorrelationId(context);
        correlationIdAccessor.CorrelationId = correlationId;
        context.Items[HeaderName] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(new ReadOnlyDictionary<string, object>(
            new Dictionary<string, object> { ["CorrelationId"] = correlationId }));

        try
        {
            await next(context);
        }
        finally
        {
            correlationIdAccessor.CorrelationId = null;
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValues)
            && headerValues.Count == 1
            && IsSafeCorrelationId(headerValues[0]))
        {
            return headerValues[0]!;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsSafeCorrelationId(string? value)
    {
        return value is { Length: > 0 and <= MaxCorrelationIdLength }
            && value.All(character => character is >= '!' and <= '~');
    }
}

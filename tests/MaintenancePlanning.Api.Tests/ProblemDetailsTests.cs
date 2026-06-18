using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class ProblemDetailsTests
{
    [Fact]
    public async Task UnhandledExceptions_ReturnSafeProblemDetailsWithCorrelationId()
    {
        await using var host = await TestApiHost.StartAsync(configureApp: app =>
        {
            app.MapGet("/throw", ThrowingEndpoint);
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/throw");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "review-correlation-123");

        var response = await host.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        Assert.Equal("Unexpected service error.", document.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "review-correlation-123",
            document.RootElement.GetProperty("correlationId").GetString());
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        Assert.DoesNotContain("private-dependency-host", body, StringComparison.Ordinal);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Contains("review-correlation-123", values);
    }

    private static IResult ThrowingEndpoint()
    {
        throw new InvalidOperationException("private-dependency-host failed.");
    }
}

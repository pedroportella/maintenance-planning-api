using System.Net;
using System.Text.Json;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class OpenApiTests
{
    [Fact]
    public async Task OpenApiDocument_IsGeneratedForHealthRoutes()
    {
        await using var host = await TestApiHost.StartAsync();

        var response = await host.Client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Maintenance Planning API", document.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.True(paths.TryGetProperty("/health/startup", out _));
        Assert.True(paths.TryGetProperty("/health/live", out _));
        Assert.True(paths.TryGetProperty("/health/ready", out _));
        Assert.True(paths.TryGetProperty("/api/v1/operations/migration-readiness", out _));
    }
}

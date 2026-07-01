using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
        Assert.True(paths.TryGetProperty("/api/v1/imports/source-work-orders", out _));
        Assert.True(paths.TryGetProperty("/api/v1/imports/maintenance-events", out _));
        Assert.True(paths.TryGetProperty("/api/v1/work-orders", out _));
        Assert.True(paths.TryGetProperty("/api/v1/work-orders/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/planning-runs", out _));
        Assert.True(paths.TryGetProperty("/api/v1/planning-runs/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/planning-runs/{id}/recommendations", out _));
        Assert.True(paths.TryGetProperty("/api/v1/packages/{id}/decisions", out _));
        Assert.True(paths.TryGetProperty("/api/v1/operations/migration-readiness", out _));
        Assert.True(paths.TryGetProperty("/api/v1/operations/posture", out _));
        Assert.True(paths.TryGetProperty("/api/v1/operations/eventing/dead-letter-replays", out _));
    }

    [Fact]
    public async Task SwaggerUi_IsEnabledInDevelopment()
    {
        await using var host = await TestApiHost.StartAsync(environmentName: Environments.Development);

        var response = await host.Client.GetAsync("/swagger");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Maintenance Planning API docs", body);
    }

    [Fact]
    public async Task SwaggerUi_IsDisabledByDefaultForProduction()
    {
        await using var host = await TestApiHost.StartAsync();

        var response = await host.Client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerUi_CanBeEnabledForProductionLikeReviewHost()
    {
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaintenancePlanning:OpenApi:SwaggerUiEnabled"] = "true"
            });
        });

        var response = await host.Client.GetAsync("/swagger");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Maintenance Planning API docs", body);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MaintenancePlanning.Api.Security;
using MaintenancePlanning.Application.Planning;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class SecurityEndpointTests
{
    [Fact]
    public async Task ProtectedRoutes_ReturnUnauthorized_WhenBearerTokenIsMissing()
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);

        var response = await host.Client.GetAsync("/api/v1/work-orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoutes_ReturnForbidden_WhenScopeDoesNotMatchPolicy()
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenNames.Importer);

        var response = await host.Client.GetAsync("/api/v1/work-orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CommandRoutes_ReturnTooManyRequests_WhenRateLimitIsExceeded()
    {
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaintenancePlanning:Security:CommandRateLimit:PermitLimit"] = "1",
                ["MaintenancePlanning:Security:CommandRateLimit:WindowSeconds"] = "300"
            });
        });

        var first = await host.Client.PostAsJsonAsync(
            "/api/v1/planning-runs",
            new CreatePlanningRunRequest());
        var second = await host.Client.PostAsJsonAsync(
            "/api/v1/planning-runs",
            new CreatePlanningRunRequest());

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }
}

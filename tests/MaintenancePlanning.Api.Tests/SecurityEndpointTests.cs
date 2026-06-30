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
    public async Task PlannerReadRoutes_AcceptReadOnlyPlannerToken()
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenNames.PlannerReadOnly);

        var response = await host.Client.GetAsync("/api/v1/work-orders");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData(TestTokenNames.PlannerReadOnly)]
    [InlineData(TestTokenNames.Importer)]
    [InlineData(TestTokenNames.Operations)]
    public async Task PlanningRunMutations_ReturnForbidden_WhenTokenLacksPlannerWriteCapability(string token)
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await host.Client.PostAsJsonAsync(
            "/api/v1/planning-runs",
            new CreatePlanningRunRequest
            {
                IdempotencyKey = "security-planning-write-negative"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(TestTokenNames.PlannerReadOnly)]
    [InlineData(TestTokenNames.Importer)]
    [InlineData(TestTokenNames.Operations)]
    public async Task PackageDecisionMutations_ReturnForbidden_WhenTokenLacksPlannerWriteCapability(string token)
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await host.Client.PostAsJsonAsync(
            $"/api/v1/packages/{Guid.NewGuid()}/decisions",
            new RecordPackageDecisionRequest
            {
                Decision = "Accepted",
                ReasonCode = "ready-for-weekly-plan"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(TestTokenNames.Planner)]
    [InlineData(TestTokenNames.PlannerReadOnly)]
    [InlineData(TestTokenNames.Operations)]
    public async Task ImportRoutes_ReturnForbidden_WhenTokenDoesNotMatchImportPolicy(string token)
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await host.Client.PostAsJsonAsync(
            "/api/v1/imports/source-work-orders",
            new
            {
                sourceSystem = "synthetic-source",
                schemaVersion = "1.0",
                idempotencyKey = "security-import-negative",
                sourceWorkOrders = Array.Empty<object>()
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(TestTokenNames.Planner)]
    [InlineData(TestTokenNames.PlannerReadOnly)]
    [InlineData(TestTokenNames.Importer)]
    public async Task OperationsRoutes_ReturnForbidden_WhenTokenDoesNotMatchOperationsPolicy(string token)
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await host.Client.GetAsync("/api/v1/operations/posture");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(TestTokenNames.Planner, "/api/v1/planning-runs", HttpStatusCode.ServiceUnavailable)]
    [InlineData(TestTokenNames.Importer, "/api/v1/imports/source-work-orders", HttpStatusCode.ServiceUnavailable)]
    public async Task ProtectedCommandRoutes_ReachEndpoint_WhenTokenMatchesPolicy(
        string token,
        string path,
        HttpStatusCode expectedStatusCode)
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await host.Client.PostAsync(path, JsonContent.Create(CreateProtectedCommandSmokeBody(path)));

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task OperationsRoutes_ReachEndpoint_WhenOperationsTokenMatchesPolicy()
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenNames.Operations);

        var response = await host.Client.GetAsync("/api/v1/operations/posture");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(HttpMethodNames.Get, "/api/v1/work-orders", HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpMethodNames.Post, "/api/v1/planning-runs", HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpMethodNames.Post, "/api/v1/imports/source-work-orders", HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpMethodNames.Get, "/api/v1/operations/posture", HttpStatusCode.OK)]
    public async Task ReviewerToken_ReachesEveryProtectedPolicy(
        string method,
        string path,
        HttpStatusCode expectedStatusCode)
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenNames.Reviewer);

        var response = await SendAsync(host.Client, method, path);

        Assert.Equal(expectedStatusCode, response.StatusCode);
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
            new CreatePlanningRunRequest
            {
                IdempotencyKey = "security-rate-limit-first"
            });
        var second = await host.Client.PostAsJsonAsync(
            "/api/v1/planning-runs",
            new CreatePlanningRunRequest
            {
                IdempotencyKey = "security-rate-limit-second"
            });

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path)
    {
        if (method == HttpMethodNames.Get)
        {
            return await client.GetAsync(path);
        }

        return await client.PostAsync(path, JsonContent.Create(CreateProtectedCommandSmokeBody(path)));
    }

    private static object CreateProtectedCommandSmokeBody(string path)
    {
        return path switch
        {
            "/api/v1/planning-runs" => new CreatePlanningRunRequest
            {
                IdempotencyKey = "security-protected-planning-command"
            },
            "/api/v1/imports/source-work-orders" => new
            {
                sourceSystem = "synthetic-source",
                schemaVersion = "1.0",
                idempotencyKey = "security-protected-command",
                sourceWorkOrders = Array.Empty<object>()
            },
            _ => throw new InvalidOperationException($"No protected command smoke body is configured for {path}.")
        };
    }

    private static class HttpMethodNames
    {
        public const string Get = "GET";
        public const string Post = "POST";
    }
}

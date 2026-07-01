using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class OpenApiTests
{
    [Fact]
    public async Task OpenApiDocument_IsGeneratedForExpectedRoutes()
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        using var document = await ReadOpenApiDocumentAsync(host);
        var paths = document.RootElement.GetProperty("paths");

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
    public async Task OpenApiDocument_DescribesBearerSecurityScheme()
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        using var document = await ReadOpenApiDocumentAsync(host);

        var bearer = document
            .RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("local-test-token", bearer.GetProperty("bearerFormat").GetString());
        Assert.Contains("synthetic local review tokens", bearer.GetProperty("description").GetString());
    }

    [Theory]
    [InlineData("/api/v1/imports/source-work-orders", "post")]
    [InlineData("/api/v1/imports/maintenance-events", "post")]
    [InlineData("/api/v1/work-orders", "get")]
    [InlineData("/api/v1/work-orders/{id}", "get")]
    [InlineData("/api/v1/planning-runs", "post")]
    [InlineData("/api/v1/planning-runs/{id}", "get")]
    [InlineData("/api/v1/planning-runs/{id}/recommendations", "get")]
    [InlineData("/api/v1/packages/{id}/decisions", "post")]
    [InlineData("/api/v1/operations/migration-readiness", "get")]
    [InlineData("/api/v1/operations/posture", "get")]
    [InlineData("/api/v1/operations/eventing/dead-letter-replays", "post")]
    public async Task OpenApiDocument_MarksProtectedOperationsWithBearerSecurity(string path, string method)
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        using var document = await ReadOpenApiDocumentAsync(host);

        var operation = GetOperation(document, path, method);

        Assert.True(HasBearerSecurity(operation), $"Expected {method.ToUpperInvariant()} {path} to require bearer security.");
    }

    [Theory]
    [InlineData("/health/startup", "get")]
    [InlineData("/health/live", "get")]
    [InlineData("/health/ready", "get")]
    public async Task OpenApiDocument_LeavesPublicHealthOperationsWithoutSecurity(string path, string method)
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        using var document = await ReadOpenApiDocumentAsync(host);

        var operation = GetOperation(document, path, method);

        Assert.False(HasBearerSecurity(operation), $"Expected {method.ToUpperInvariant()} {path} to stay public.");
    }

    [Theory]
    [InlineData("/health/startup", "get", "Get startup health", "Public probe for startup completion.")]
    [InlineData("/health/live", "get", "Get liveness health", "API process is running")]
    [InlineData("/health/ready", "get", "Get readiness health", "Public probe for dependencies required to handle local review traffic.")]
    [InlineData("/api/v1/imports/source-work-orders", "post", "Import synthetic source work orders", "source-system-shaped work-order batches")]
    [InlineData("/api/v1/imports/maintenance-events", "post", "Import synthetic maintenance events", "versioned maintenance-event batches")]
    [InlineData("/api/v1/work-orders", "get", "List work orders", "filtered backlog page")]
    [InlineData("/api/v1/work-orders/{id}", "get", "Get work-order detail", "one work order with planning readiness")]
    [InlineData("/api/v1/planning-runs", "post", "Create a planning run", "deterministic package recommendations")]
    [InlineData("/api/v1/planning-runs/{id}", "get", "Get planning run status", "status, horizon and recommendation counts")]
    [InlineData("/api/v1/planning-runs/{id}/recommendations", "get", "List planning run recommendations", "recommended packages, blockers, scores")]
    [InlineData("/api/v1/packages/{id}/decisions", "post", "Record a package decision", "audits an accepted, rejected or deferred recommendation decision")]
    [InlineData("/api/v1/operations/migration-readiness", "get", "Check migration readiness", "without applying migrations")]
    [InlineData("/api/v1/operations/posture", "get", "Get operations posture", "import freshness, stale received imports")]
    [InlineData("/api/v1/operations/eventing/dead-letter-replays", "post", "Start a dead-letter replay", "records a replay audit")]
    public async Task OpenApiDocument_DescribesOperations(
        string path,
        string method,
        string expectedSummary,
        string expectedDescriptionSnippet)
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        using var document = await ReadOpenApiDocumentAsync(host);

        var operation = GetOperation(document, path, method);

        Assert.Equal(expectedSummary, GetRequiredString(operation, "summary"));
        Assert.Contains(expectedDescriptionSnippet, GetRequiredString(operation, "description"));
    }

    [Theory]
    [InlineData("Health", "Public startup, liveness and readiness probes")]
    [InlineData("Imports", "Protected synthetic source-system-shaped import feeds")]
    [InlineData("Operations", "Protected operational readiness, posture and dead-letter replay controls")]
    [InlineData("Planning", "Protected planning runs, package recommendations")]
    [InlineData("Work Orders", "Protected planner-facing work-order backlog")]
    public async Task OpenApiDocument_DescribesRouteTags(string tagName, string expectedDescriptionSnippet)
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        using var document = await ReadOpenApiDocumentAsync(host);

        var tags = document.RootElement.GetProperty("tags");

        Assert.True(
            tags.EnumerateArray().Any(tag =>
                string.Equals(tag.GetProperty("name").GetString(), tagName, StringComparison.Ordinal)
                && (tag.GetProperty("description").GetString()?.Contains(expectedDescriptionSnippet, StringComparison.Ordinal) ?? false)),
            $"Expected tag {tagName} to include description snippet {expectedDescriptionSnippet}.");
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

    private static async Task<JsonDocument> ReadOpenApiDocumentAsync(TestApiHost host)
    {
        var response = await host.Client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(body);
    }

    private static JsonElement GetOperation(JsonDocument document, string path, string method)
    {
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty(path, out var pathItem), $"Expected OpenAPI path {path}.");
        Assert.True(pathItem.TryGetProperty(method, out var operation), $"Expected {method.ToUpperInvariant()} operation for {path}.");

        return operation;
    }

    private static bool HasBearerSecurity(JsonElement operation)
    {
        if (!operation.TryGetProperty("security", out var security) || security.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return security.EnumerateArray().Any(requirement =>
            requirement.TryGetProperty("Bearer", out var scopes)
            && scopes.ValueKind == JsonValueKind.Array);
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        Assert.True(element.TryGetProperty(propertyName, out var value), $"Expected {propertyName} to be present.");

        var text = value.GetString();

        Assert.False(string.IsNullOrWhiteSpace(text), $"Expected {propertyName} to be populated.");

        return text ?? string.Empty;
    }
}

using System.Net;
using MaintenancePlanning.Application.Readiness;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class HealthEndpointTests
{
    [Theory]
    [InlineData("/health/startup")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_ReturnHealthyResponses(string path)
    {
        await using var host = await TestApiHost.StartAsync();

        var response = await host.Client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"Healthy\"", body, StringComparison.Ordinal);
        Assert.Contains("\"correlationId\":", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Liveness_DoesNotDependOnReadinessDependencies()
    {
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IReadinessProbe, UnreadyProbe>();
        });

        var liveResponse = await host.Client.GetAsync("/health/live");
        var readyResponse = await host.Client.GetAsync("/health/ready");
        var readyBody = await readyResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
        Assert.Contains("\"status\":\"Unhealthy\"", readyBody, StringComparison.Ordinal);
        Assert.DoesNotContain(UnreadyProbe.InternalDependencyName, readyBody, StringComparison.Ordinal);
    }

    private sealed class UnreadyProbe : IReadinessProbe
    {
        public const string InternalDependencyName = "private-dependency-host";

        public Task<ReadinessSnapshot> CheckAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ReadinessSnapshot.NotReady(new ReadinessDependency(InternalDependencyName, false)));
        }
    }
}

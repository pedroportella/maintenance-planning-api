using System.Net;
using MaintenancePlanning.Application.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class OperationsEndpointTests
{
    [Fact]
    public async Task MigrationReadiness_ReturnsNotConfiguredReport_WhenDatabaseIsNotConfigured()
    {
        await using var host = await TestApiHost.StartAsync();

        var response = await host.Client.GetAsync("/api/v1/operations/migration-readiness");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"databaseConfigured\":false", body, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"database-not-configured\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrationReadiness_ReturnsUnavailable_WhenReporterIsNotReady()
    {
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IMigrationReadinessReporter, PendingMigrationReporter>();
        });

        var response = await host.Client.GetAsync("/api/v1/operations/migration-readiness");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("\"status\":\"pending-migrations\"", body, StringComparison.Ordinal);
        Assert.Contains("\"pendingMigrationCount\":1", body, StringComparison.Ordinal);
    }

    private sealed class PendingMigrationReporter : IMigrationReadinessReporter
    {
        public Task<MigrationReadinessReport> CheckAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new MigrationReadinessReport(
                DatabaseConfigured: true,
                DatabaseReachable: true,
                IsReady: false,
                Status: "pending-migrations",
                AppliedMigrationCount: 0,
                PendingMigrationCount: 1,
                PendingMigrations: new[] { "20260619000000_Test" },
                LatestAppliedMigration: null,
                IssueCode: "pending-migrations",
                CheckedAtUtc: DateTimeOffset.UnixEpoch));
        }
    }
}

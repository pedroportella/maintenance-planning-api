using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MaintenancePlanning.Api.Security;
using MaintenancePlanning.Application.Eventing;
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

    [Fact]
    public async Task Posture_ReturnsUnconfiguredState_WhenDatabaseIsNotConfigured()
    {
        await using var host = await TestApiHost.StartAsync();

        var response = await host.Client.GetAsync("/api/v1/operations/posture");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(document.RootElement.GetProperty("databaseConfigured").GetBoolean());
        Assert.Equal(
            "degraded",
            document.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "import-persistence-not-configured",
            document.RootElement.GetProperty("issueCode").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("latestImport").ValueKind);
        Assert.Equal("not-configured", document.RootElement.GetProperty("eventing").GetProperty("publishMode").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("eventing").GetProperty("deadLetterCount").GetInt32());
    }

    [Fact]
    public async Task DeadLetterReplay_StartsReplay_WhenOperationsTokenIsProvided()
    {
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IDeadLetterReplayService>(new SuccessfulReplayService());
        });

        var response = await host.Client.PostAsJsonAsync(
            "/api/v1/operations/eventing/dead-letter-replays",
            new StartDeadLetterReplayRequest
            {
                ReasonCode = "review-dlq-retry",
                RequestedBy = "operations-review"
            });
        var replay = await response.Content.ReadFromJsonAsync<DeadLetterReplayResult>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(replay);
        Assert.Equal("started", replay.Status);
        Assert.Equal("review-dlq-retry", replay.ReasonCode);
    }

    [Fact]
    public async Task DeadLetterReplay_ReturnsForbidden_WhenPlannerTokenIsProvided()
    {
        await using var host = await TestApiHost.StartAsync(authenticated: false);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenNames.Planner);

        var response = await host.Client.PostAsJsonAsync(
            "/api/v1/operations/eventing/dead-letter-replays",
            new StartDeadLetterReplayRequest
            {
                ReasonCode = "review-dlq-retry"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private sealed class SuccessfulReplayService : IDeadLetterReplayService
    {
        public Task<DeadLetterReplayOutcome> StartReplayAsync(
            StartDeadLetterReplayRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(DeadLetterReplayOutcome.Success(new DeadLetterReplayResult(
                Guid.NewGuid(),
                "started",
                "replay-task-1",
                "arn:aws:sqs:ap-southeast-2:000000000000:review-work-dlq",
                "arn:aws:sqs:ap-southeast-2:000000000000:review-work",
                request.ReasonCode,
                request.RequestedBy ?? "local-review",
                DateTimeOffset.UnixEpoch)));
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MaintenancePlanning.Api.Security;
using MaintenancePlanning.Application.Eventing;
using MaintenancePlanning.Application.Imports;
using MaintenancePlanning.Application.Persistence;
using Microsoft.Extensions.Configuration;
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
        Assert.Equal(0, document.RootElement.GetProperty("staleReceivedImports").GetProperty("count").GetInt32());
        Assert.Equal(
            30,
            document.RootElement.GetProperty("staleReceivedImports").GetProperty("thresholdMinutes").GetInt32());
        Assert.Equal("not-configured", document.RootElement.GetProperty("eventing").GetProperty("publishMode").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("eventing").GetProperty("deadLetterCount").GetInt32());
        Assert.False(document.RootElement.GetProperty("outbox").GetProperty("configured").GetBoolean());
        Assert.Equal(0, document.RootElement.GetProperty("outbox").GetProperty("pendingCount").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("outbox").GetProperty("failedCount").GetInt32());
    }

    [Fact]
    public async Task Posture_ReturnsStaleImportAndOutboxCounts_WhenStoresReportIssues()
    {
        var oldestReceivedAtUtc = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaintenancePlanning:Operations:StaleReceivedImportThresholdMinutes"] = "15"
            });
            builder.Services.AddSingleton<IImportStore>(new PostureImportStore(
                new StaleReceivedImportSummary(2, oldestReceivedAtUtc)));
            builder.Services.AddSingleton<IOutboxStore>(new PostureOutboxStore(
                new OutboxPostureSummary(3, 1, "eventbridge-throttled")));
        });

        var response = await host.Client.GetAsync("/api/v1/operations/posture");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(document.RootElement.GetProperty("databaseConfigured").GetBoolean());
        Assert.Equal("degraded", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("stale-received-imports", document.RootElement.GetProperty("issueCode").GetString());

        var staleReceivedImports = document.RootElement.GetProperty("staleReceivedImports");
        Assert.Equal(2, staleReceivedImports.GetProperty("count").GetInt32());
        Assert.Equal(15, staleReceivedImports.GetProperty("thresholdMinutes").GetInt32());
        Assert.Equal(oldestReceivedAtUtc, staleReceivedImports.GetProperty("oldestReceivedAtUtc").GetDateTimeOffset());

        var outbox = document.RootElement.GetProperty("outbox");
        Assert.True(outbox.GetProperty("configured").GetBoolean());
        Assert.Equal(3, outbox.GetProperty("pendingCount").GetInt32());
        Assert.Equal(1, outbox.GetProperty("failedCount").GetInt32());
        Assert.Equal("eventbridge-throttled", outbox.GetProperty("lastFailureCode").GetString());
    }

    [Fact]
    public async Task Posture_UsesOutboxIssueCode_WhenOnlyOutboxFailuresAreReported()
    {
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IImportStore>(new PostureImportStore(
                new StaleReceivedImportSummary(0, null)));
            builder.Services.AddSingleton<IOutboxStore>(new PostureOutboxStore(
                new OutboxPostureSummary(0, 2, "eventbridge-unavailable")));
        });

        var response = await host.Client.GetAsync("/api/v1/operations/posture");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("degraded", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("outbox-failed-events", document.RootElement.GetProperty("issueCode").GetString());
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

    private sealed class PostureImportStore(StaleReceivedImportSummary staleReceivedImports) : IImportStore
    {
        public bool IsConfigured => true;

        public Task<StoredImport?> FindImportAsync(
            string sourceSystem,
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<StoredImport?>(null);

        public Task<StoredImport?> FindLatestImportAsync(CancellationToken cancellationToken) =>
            Task.FromResult<StoredImport?>(null);

        public Task<StoredImport?> FindLatestFailedImportAsync(CancellationToken cancellationToken) =>
            Task.FromResult<StoredImport?>(null);

        public Task<StaleReceivedImportSummary> CountStaleReceivedImportsAsync(
            DateTimeOffset staleBeforeUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(staleReceivedImports);

        public Task<bool> HasEventIdempotencyKeyAsync(
            string sourceSystem,
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> HasEventIdAsync(
            string eventId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<StoredSourceRecord?> FindWorkOrderAsync(
            string sourceSystem,
            string sourceId,
            CancellationToken cancellationToken) =>
            Task.FromResult<StoredSourceRecord?>(null);

        public Task SaveImportAsync(
            ImportPersistenceBatch batch,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("This store is only used for operations posture tests.");

        public Task UpdateImportStatusAsync(
            Guid importId,
            string status,
            DateTimeOffset completedAtUtc,
            string? failureCode,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("This store is only used for operations posture tests.");
    }

    private sealed class PostureOutboxStore(OutboxPostureSummary posture) : IOutboxStore
    {
        public bool IsConfigured => true;

        public Task<IReadOnlyList<OutboxMessage>> LoadPendingAsync(
            int maxEvents,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OutboxMessage>>(Array.Empty<OutboxMessage>());

        public Task<OutboxPostureSummary> CheckPostureAsync(CancellationToken cancellationToken) =>
            Task.FromResult(posture);

        public Task MarkPublishedAsync(
            Guid outboxEventId,
            DateTimeOffset publishedAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("This store is only used for operations posture tests.");

        public Task MarkFailedAsync(
            Guid outboxEventId,
            string errorCode,
            DateTimeOffset nextAvailableAtUtc,
            int maxAttempts,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("This store is only used for operations posture tests.");
    }
}

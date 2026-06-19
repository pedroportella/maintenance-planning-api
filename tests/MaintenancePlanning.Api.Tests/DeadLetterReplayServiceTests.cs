using MaintenancePlanning.Application.Eventing;
using MaintenancePlanning.Application.Imports;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class DeadLetterReplayServiceTests
{
    [Fact]
    public async Task StartReplayAsync_StartsReplayAndRecordsAudit()
    {
        var importStore = new InMemoryImportStore();
        var replayClient = new RecordingReplayClient();
        var service = new DeadLetterReplayService(replayClient, importStore);

        var outcome = await service.StartReplayAsync(
            new StartDeadLetterReplayRequest
            {
                ReasonCode = "review-dlq-retry",
                RequestedBy = "operations-review",
                MaxMessagesPerSecond = 5
            },
            CancellationToken.None);

        Assert.NotNull(outcome.Result);
        Assert.Null(outcome.Problem);
        Assert.Equal("started", outcome.Result.Status);
        Assert.Equal("review-dlq-retry", outcome.Result.ReasonCode);
        Assert.Equal("operations-review", outcome.Result.RequestedBy);
        Assert.Equal(5, replayClient.MaxMessagesPerSecond);
        var audit = Assert.Single(importStore.Imports);
        Assert.Equal("eventing", audit.SourceSystem);
        Assert.Equal("dlq-replay", audit.ImportKind);
        Assert.Equal("Completed", audit.Status);
    }

    [Fact]
    public async Task StartReplayAsync_ReturnsValidationProblem_WhenReasonIsMissing()
    {
        var service = new DeadLetterReplayService(
            new RecordingReplayClient(),
            new InMemoryImportStore());

        var outcome = await service.StartReplayAsync(
            new StartDeadLetterReplayRequest(),
            CancellationToken.None);

        Assert.Null(outcome.Result);
        Assert.NotNull(outcome.Problem);
        Assert.Equal(422, outcome.Problem.StatusCode);
        Assert.Equal("dead-letter-replay-validation-failed", outcome.Problem.Code);
        Assert.Contains("reasonCode", outcome.Problem.Errors!.Keys);
    }

    private sealed class RecordingReplayClient : IDeadLetterReplayClient
    {
        public bool IsConfigured => true;

        public int? MaxMessagesPerSecond { get; private set; }

        public Task<DeadLetterMoveTask> StartReplayAsync(
            int? maxMessagesPerSecond,
            CancellationToken cancellationToken)
        {
            MaxMessagesPerSecond = maxMessagesPerSecond;

            return Task.FromResult(new DeadLetterMoveTask(
                "replay-task-1",
                "arn:aws:sqs:ap-southeast-2:000000000000:review-work-dlq",
                "arn:aws:sqs:ap-southeast-2:000000000000:review-work"));
        }
    }

    private sealed class InMemoryImportStore : IImportStore
    {
        public bool IsConfigured => true;

        public List<StoredImport> Imports { get; } = new();

        public Task<StoredImport?> FindImportAsync(
            string sourceSystem,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Imports.SingleOrDefault(
                item => item.SourceSystem == sourceSystem && item.IdempotencyKey == idempotencyKey));
        }

        public Task<StoredImport?> FindLatestImportAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Imports.OrderByDescending(item => item.ReceivedAtUtc).FirstOrDefault());
        }

        public Task<StoredImport?> FindLatestFailedImportAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Imports
                .Where(item => item.Status == "Failed")
                .OrderByDescending(item => item.ReceivedAtUtc)
                .FirstOrDefault());
        }

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
            CancellationToken cancellationToken)
        {
            Imports.Add(new StoredImport(
                batch.Import.Id,
                batch.Import.SourceSystem,
                batch.Import.ImportKind,
                batch.Import.IdempotencyKey,
                batch.Import.RequestHash,
                batch.Import.Status,
                batch.Import.ReceivedCount,
                batch.Import.AcceptedCount,
                batch.Import.RejectedCount,
                batch.Import.IgnoredDuplicateCount,
                batch.Import.IgnoredStaleCount,
                batch.Import.FailureCode,
                batch.Import.ReceivedAtUtc,
                batch.Import.CompletedAtUtc,
                Array.Empty<ImportEventResult>()));

            return Task.CompletedTask;
        }
    }
}

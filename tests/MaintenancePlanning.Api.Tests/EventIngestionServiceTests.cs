using System.Text.Json;
using MaintenancePlanning.Application.Eventing;
using MaintenancePlanning.Application.Imports;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class EventIngestionServiceTests
{
    private static readonly DateTimeOffset ReferenceTime = new(2026, 01, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProcessAsync_ImportsEventBridgeMessageAndReplaysRedelivery()
    {
        var store = new InMemoryImportStore();
        var service = CreateService(store);
        var message = CreateEventBridgeMessage(
            "eventbridge-0001",
            WorkOrderEvent("evt-worker-0001", "worker:WO-9000:create", CreateWorkOrder("WO-9000")));

        var first = await service.ProcessAsync(message, CancellationToken.None);
        var second = await service.ProcessAsync(message, CancellationToken.None);

        Assert.Equal("processed", first.Status);
        Assert.True(first.DeleteMessage);
        Assert.NotNull(first.ImportResult);
        Assert.False(first.ImportResult.DuplicateRequest);
        Assert.Equal(1, first.ImportResult.AcceptedCount);
        Assert.NotNull(second.ImportResult);
        Assert.True(second.ImportResult.DuplicateRequest);
        Assert.Equal(1, store.ImportCount);
        Assert.Equal(1, store.WorkOrderCount);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresDuplicateEventWithDifferentEventBridgeId()
    {
        var store = new InMemoryImportStore();
        var service = CreateService(store);
        var envelope = WorkOrderEvent("evt-worker-0002", "worker:WO-9001:create", CreateWorkOrder("WO-9001"));

        var first = await service.ProcessAsync(CreateEventBridgeMessage("eventbridge-0002a", envelope), CancellationToken.None);
        var second = await service.ProcessAsync(CreateEventBridgeMessage("eventbridge-0002b", envelope), CancellationToken.None);

        Assert.NotNull(first.ImportResult);
        Assert.Equal(1, first.ImportResult.AcceptedCount);
        Assert.NotNull(second.ImportResult);
        Assert.Equal(0, second.ImportResult.AcceptedCount);
        Assert.Equal(1, second.ImportResult.IgnoredDuplicateCount);
        Assert.Equal(2, store.ImportCount);
        Assert.Equal(1, store.WorkOrderCount);
    }

    [Fact]
    public async Task ProcessAsync_AuditsMalformedMessageAndDeletesIt()
    {
        var store = new InMemoryImportStore();
        var service = CreateService(store);
        var message = new QueuedEventMessage("malformed-message", "receipt", "{", ApproximateReceiveCount: 1);

        var result = await service.ProcessAsync(message, CancellationToken.None);
        var failedImport = await store.FindLatestFailedImportAsync(CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.True(result.DeleteMessage);
        Assert.Equal("event-message-invalid", result.FailureCode);
        Assert.NotNull(failedImport);
        Assert.Equal("Failed", failedImport.Status);
        Assert.Equal("event-message-invalid", failedImport.FailureCode);
    }

    private static EventIngestionService CreateService(InMemoryImportStore store)
    {
        return new EventIngestionService(new ImportService(store), store);
    }

    private static QueuedEventMessage CreateEventBridgeMessage(
        string eventBridgeId,
        MaintenanceEventEnvelope envelope)
    {
        var body = JsonSerializer.Serialize(new
        {
            version = "0",
            id = eventBridgeId,
            source = "maintenance-data-simulator",
            account = "000000000000",
            time = ReferenceTime,
            region = "ap-southeast-2",
            resources = Array.Empty<string>(),
            detail = envelope
        });

        return new QueuedEventMessage($"message-{eventBridgeId}", $"receipt-{eventBridgeId}", body, ApproximateReceiveCount: 1);
    }

    private static MaintenanceEventEnvelope WorkOrderEvent(
        string eventId,
        string idempotencyKey,
        SourceWorkOrderPayload payload)
    {
        return new MaintenanceEventEnvelope
        {
            EventId = eventId,
            EventType = "WorkOrderCreated",
            SchemaVersion = "1.0",
            SourceSystem = "synthetic-source",
            SourceRecordId = payload.SourceId,
            CorrelationId = $"corr-{eventId}",
            OccurredAt = ReferenceTime.AddMinutes(1),
            PublishedAt = ReferenceTime.AddMinutes(2),
            IdempotencyKey = idempotencyKey,
            Payload = JsonSerializer.SerializeToElement(payload)
        };
    }

    private static SourceWorkOrderPayload CreateWorkOrder(string sourceId)
    {
        return new SourceWorkOrderPayload
        {
            SourceSystem = "synthetic-source",
            SourceId = sourceId,
            WorkOrderNumber = sourceId,
            Title = "Inspect packaged work scope",
            WorkType = "corrective",
            Priority = "high",
            LifecycleStatus = "ReadyForPlanning",
            AssetSourceId = "ASSET-9000",
            FunctionalLocationSourceId = "FL-9000",
            RequiredStartUtc = ReferenceTime.AddDays(1),
            DueAtUtc = ReferenceTime.AddDays(4),
            ScheduledStartUtc = null,
            EstimatedHours = 6.5m,
            SourceUpdatedAtUtc = ReferenceTime.AddMinutes(1),
            SourceDataReadiness = new SourceDataReadinessContract
            {
                Status = "Ready"
            },
            ValidationIssues = Array.Empty<ValidationIssueContract>()
        };
    }

    private sealed class InMemoryImportStore : IImportStore
    {
        private readonly List<StoredImport> _imports = new();
        private readonly List<IntegrationEventAuditRecord> _events = new();
        private readonly Dictionary<string, StoredSourceRecord> _workOrders = new(StringComparer.Ordinal);

        public bool IsConfigured => true;

        public int ImportCount => _imports.Count;

        public int WorkOrderCount => _workOrders.Count;

        public Task<StoredImport?> FindImportAsync(
            string sourceSystem,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_imports.SingleOrDefault(
                item => item.SourceSystem == sourceSystem && item.IdempotencyKey == idempotencyKey));
        }

        public Task<StoredImport?> FindLatestImportAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_imports.OrderByDescending(item => item.ReceivedAtUtc).FirstOrDefault());
        }

        public Task<StoredImport?> FindLatestFailedImportAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_imports
                .Where(item => item.Status == "Failed")
                .OrderByDescending(item => item.ReceivedAtUtc)
                .FirstOrDefault());
        }

        public Task<bool> HasEventIdempotencyKeyAsync(
            string sourceSystem,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_events.Any(
                item => item.SourceSystem == sourceSystem && item.IdempotencyKey == idempotencyKey));
        }

        public Task<bool> HasEventIdAsync(
            string eventId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_events.Any(item => item.EventId == eventId));
        }

        public Task<StoredSourceRecord?> FindWorkOrderAsync(
            string sourceSystem,
            string sourceId,
            CancellationToken cancellationToken)
        {
            _workOrders.TryGetValue(SourceRecordKey(sourceSystem, sourceId), out var record);
            return Task.FromResult(record);
        }

        public Task SaveImportAsync(
            ImportPersistenceBatch batch,
            CancellationToken cancellationToken)
        {
            _events.AddRange(batch.Events);
            foreach (var workOrder in batch.WorkOrders)
            {
                _workOrders[SourceRecordKey(workOrder.SourceSystem, workOrder.SourceId)] =
                    new StoredSourceRecord(workOrder.SourceSystem, workOrder.SourceId, workOrder.SourceUpdatedAtUtc);
            }

            _imports.Add(new StoredImport(
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
                batch.Events.Select(item => new ImportEventResult(
                    item.EventId,
                    item.EventType,
                    item.SourceRecordId,
                    item.IdempotencyKey,
                    item.Disposition,
                    item.Status,
                    item.Readiness,
                    item.ValidationIssueCode)).ToArray()));

            return Task.CompletedTask;
        }

        private static string SourceRecordKey(string sourceSystem, string sourceId)
        {
            return $"{sourceSystem}|{sourceId}";
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MaintenancePlanning.Application.Imports;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class ImportEndpointTests
{
    private static readonly DateTimeOffset ReferenceTime = new(2026, 01, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SourceWorkOrders_ReturnsValidationProblem_WhenPayloadIsInvalid()
    {
        var store = new InMemoryImportStore();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IImportStore>(store);
        });

        var response = await host.Client.PostAsJsonAsync(
            "/api/v1/imports/source-work-orders",
            new
            {
                sourceSystem = "synthetic-source",
                schemaVersion = "1.0",
                idempotencyKey = "invalid-source-work-orders",
                sourceWorkOrders = new[]
                {
                    new
                    {
                        sourceSystem = "synthetic-source",
                        sourceId = "WO-4000",
                        title = "Missing required work order fields",
                        lifecycleStatus = "Imported",
                        sourceUpdatedAtUtc = ReferenceTime,
                        sourceDataReadiness = new
                        {
                            status = "Ready"
                        }
                    }
                }
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"import-validation-failed\"", body, StringComparison.Ordinal);
        Assert.Contains("workOrderNumber", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SourceWorkOrders_ReplaysDuplicateRequestWithoutCreatingNewRows()
    {
        var store = new InMemoryImportStore();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IImportStore>(store);
        });
        var request = CreateSourceWorkOrderRequest("source-work-orders-idempotent-1");

        var firstResponse = await host.Client.PostAsJsonAsync("/api/v1/imports/source-work-orders", request);
        var secondResponse = await host.Client.PostAsJsonAsync("/api/v1/imports/source-work-orders", request);
        var first = await firstResponse.Content.ReadFromJsonAsync<ImportResult>();
        var second = await secondResponse.Content.ReadFromJsonAsync<ImportResult>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.False(first.DuplicateRequest);
        Assert.True(second.DuplicateRequest);
        Assert.Equal(2, first.ReceivedCount);
        Assert.Equal(2, first.AcceptedCount);
        Assert.Equal(0, first.RejectedCount);
        Assert.Equal(2, store.WorkOrderCount);
        Assert.Equal(1, store.ImportCount);
    }

    [Fact]
    public async Task SourceWorkOrders_ReturnsConflict_WhenIdempotencyKeyHasDifferentBody()
    {
        var store = new InMemoryImportStore();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IImportStore>(store);
        });
        var firstRequest = CreateSourceWorkOrderRequest("source-work-orders-conflict-1");
        var secondRequest = CreateSourceWorkOrderRequest("source-work-orders-conflict-1", title: "Changed title");

        var firstResponse = await host.Client.PostAsJsonAsync("/api/v1/imports/source-work-orders", firstRequest);
        var secondResponse = await host.Client.PostAsJsonAsync("/api/v1/imports/source-work-orders", secondRequest);
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Contains("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"idempotency-conflict\"", body, StringComparison.Ordinal);
        Assert.Equal(1, store.ImportCount);
    }

    [Fact]
    public async Task MaintenanceEvents_ImportsAcceptedRejectedDuplicateAndStaleEvents()
    {
        var store = new InMemoryImportStore();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IImportStore>(store);
        });
        var request = CreateMaintenanceEventsRequest();

        var response = await host.Client.PostAsJsonAsync("/api/v1/imports/maintenance-events", request);
        var result = await response.Content.ReadFromJsonAsync<ImportResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("maintenance-events", result.ImportKind);
        Assert.Equal(8, result.ReceivedCount);
        Assert.Equal(5, result.AcceptedCount);
        Assert.Equal(1, result.RejectedCount);
        Assert.Equal(1, result.IgnoredDuplicateCount);
        Assert.Equal(1, result.IgnoredStaleCount);
        Assert.Contains(result.Events, item => item.Disposition == "accepted-blocked" && item.Readiness == "Blocked");
        Assert.Contains(result.Events, item => item.Disposition == "rejected" && item.ValidationIssueCode == "missing-functional-location");
        Assert.Contains(result.Events, item => item.Disposition == "ignored-duplicate");
        Assert.Contains(result.Events, item => item.Disposition == "ignored-stale");
        Assert.Equal(2, store.WorkOrderCount);
        Assert.Equal(1, store.MajorEventCount);
        Assert.Equal(1, store.ImportCount);

        var postureResponse = await host.Client.GetAsync("/api/v1/operations/posture");
        var postureBody = await postureResponse.Content.ReadAsStringAsync();
        using var postureDocument = JsonDocument.Parse(postureBody);
        var latestImport = postureDocument.RootElement.GetProperty("latestImport");

        Assert.Equal(HttpStatusCode.OK, postureResponse.StatusCode);
        Assert.Equal("maintenance-events", latestImport.GetProperty("importKind").GetString());
        Assert.Equal("baseline-week-contract-import-1", latestImport.GetProperty("idempotencyKey").GetString());
        Assert.Equal(8, latestImport.GetProperty("receivedCount").GetInt32());
        Assert.Equal(1, latestImport.GetProperty("ignoredDuplicateCount").GetInt32());
    }

    [Fact]
    public async Task Imports_ReturnServiceUnavailable_WhenPersistenceIsNotConfigured()
    {
        await using var host = await TestApiHost.StartAsync();

        var response = await host.Client.PostAsJsonAsync(
            "/api/v1/imports/source-work-orders",
            CreateSourceWorkOrderRequest("source-work-orders-no-store"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"import-persistence-not-configured\"", body, StringComparison.Ordinal);
    }

    private static SourceWorkOrderImportRequest CreateSourceWorkOrderRequest(
        string idempotencyKey,
        string title = "Replace pump seals")
    {
        return new SourceWorkOrderImportRequest
        {
            SourceSystem = "synthetic-source",
            SchemaVersion = "1.0",
            IdempotencyKey = idempotencyKey,
            SourceWorkOrders = new[]
            {
                CreateWorkOrder("WO-3000", title, "Ready", ReferenceTime.AddMinutes(1), estimatedHours: 8.5m),
                CreateWorkOrder(
                    "WO-3001",
                    "Inspect standby valve",
                    "Blocked",
                    ReferenceTime.AddMinutes(2),
                    estimatedHours: null,
                    issueCode: "missing-estimate",
                    issueSeverity: "warning")
            }
        };
    }

    private static MaintenanceEventImportRequest CreateMaintenanceEventsRequest()
    {
        return new MaintenanceEventImportRequest
        {
            SourceSystem = "synthetic-source",
            SchemaVersion = "1.0",
            BatchIdempotencyKey = "baseline-week-contract-import-1",
            Events = new[]
            {
                WorkOrderEvent(
                    "evt-baseline-week-0001",
                    "WorkOrderCreated",
                    "baseline-week:WO-3000:create",
                    CreateWorkOrder("WO-3000", "Replace pump seals", "Ready", ReferenceTime.AddMinutes(1), estimatedHours: 8.5m),
                    occurredAt: ReferenceTime.AddMinutes(1)),
                WorkOrderEvent(
                    "evt-baseline-week-0002",
                    "WorkOrderCreated",
                    "baseline-week:WO-3001:create",
                    CreateWorkOrder(
                        "WO-3001",
                        "Inspect standby valve",
                        "Blocked",
                        ReferenceTime.AddMinutes(2),
                        estimatedHours: null,
                        issueCode: "missing-estimate",
                        issueSeverity: "warning"),
                    occurredAt: ReferenceTime.AddMinutes(2)),
                WorkOrderEvent(
                    "evt-baseline-week-0003",
                    "WorkOrderCreated",
                    "baseline-week:WO-3002:create",
                    CreateWorkOrder(
                        "WO-3002",
                        "Check auxiliary drive",
                        "Blocked",
                        ReferenceTime.AddMinutes(3),
                        estimatedHours: 4m,
                        issueCode: "missing-functional-location",
                        issueSeverity: "error",
                        functionalLocationSourceId: null),
                    occurredAt: ReferenceTime.AddMinutes(3)),
                WorkOrderEvent(
                    "evt-baseline-week-0004",
                    "WorkOrderUpdated",
                    "baseline-week:WO-3000:update:stale",
                    CreateWorkOrder("WO-3000", "Replace pump seals stale", "Ready", ReferenceTime.AddMinutes(-5), estimatedHours: 8.5m),
                    occurredAt: ReferenceTime.AddMinutes(4)),
                WorkOrderEvent(
                    "evt-baseline-week-0005",
                    "WorkOrderCreated",
                    "baseline-week:WO-3000:create",
                    CreateWorkOrder("WO-3000", "Replace pump seals", "Ready", ReferenceTime.AddMinutes(1), estimatedHours: 8.5m),
                    occurredAt: ReferenceTime.AddMinutes(5)),
                MajorEvent(
                    "evt-baseline-week-0006",
                    "baseline-week:EVT-3000:window",
                    occurredAt: ReferenceTime.AddMinutes(6)),
                PartsEvent(
                    "evt-baseline-week-0007",
                    "baseline-week:PART-3000-WO-3000",
                    occurredAt: ReferenceTime.AddMinutes(7)),
                WorkOrderEvent(
                    "evt-baseline-week-0008",
                    "WorkOrderStatusChanged",
                    "baseline-week:WO-3001:status",
                    CreateWorkOrder(
                        "WO-3001",
                        "Inspect standby valve",
                        "Blocked",
                        ReferenceTime.AddMinutes(8),
                        estimatedHours: null,
                        issueCode: "missing-estimate",
                        issueSeverity: "warning"),
                    occurredAt: ReferenceTime.AddMinutes(8))
            }
        };
    }

    private static SourceWorkOrderPayload CreateWorkOrder(
        string sourceId,
        string title,
        string readiness,
        DateTimeOffset sourceUpdatedAtUtc,
        decimal? estimatedHours,
        string? issueCode = null,
        string issueSeverity = "warning",
        string? functionalLocationSourceId = "FL-3000")
    {
        var issues = issueCode is null
            ? Array.Empty<ValidationIssueContract>()
            : new[]
            {
                new ValidationIssueContract
                {
                    Code = issueCode,
                    Severity = issueSeverity,
                    SourceField = issueCode == "missing-estimate" ? "estimatedHours" : "functionalLocationSourceId",
                    Detail = "Synthetic source-field issue for import contract testing."
                }
            };

        return new SourceWorkOrderPayload
        {
            SourceSystem = "synthetic-source",
            SourceId = sourceId,
            WorkOrderNumber = sourceId,
            Title = title,
            WorkType = "corrective",
            Priority = "high",
            LifecycleStatus = readiness == "Ready" ? "ReadyForPlanning" : "Imported",
            AssetSourceId = "ASSET-3000",
            FunctionalLocationSourceId = functionalLocationSourceId,
            RequiredStartUtc = ReferenceTime.AddDays(5),
            DueAtUtc = ReferenceTime.AddDays(8),
            ScheduledStartUtc = null,
            EstimatedHours = estimatedHours,
            SourceUpdatedAtUtc = sourceUpdatedAtUtc,
            SourceDataReadiness = new SourceDataReadinessContract
            {
                Status = readiness,
                IssueCode = issueCode,
                IssueDetail = issueCode is null ? null : "Synthetic issue retained for planner review.",
                ValidationIssues = issues
            },
            ValidationIssues = issues
        };
    }

    private static MaintenanceEventEnvelope WorkOrderEvent(
        string eventId,
        string eventType,
        string idempotencyKey,
        SourceWorkOrderPayload payload,
        DateTimeOffset occurredAt)
    {
        return new MaintenanceEventEnvelope
        {
            EventId = eventId,
            EventType = eventType,
            SchemaVersion = "1.0",
            SourceSystem = "synthetic-source",
            SourceRecordId = payload.SourceId,
            CorrelationId = $"corr-{eventId}",
            OccurredAt = occurredAt,
            PublishedAt = occurredAt.AddSeconds(10),
            IdempotencyKey = idempotencyKey,
            Payload = JsonSerializer.SerializeToElement(payload)
        };
    }

    private static MaintenanceEventEnvelope MajorEvent(
        string eventId,
        string idempotencyKey,
        DateTimeOffset occurredAt)
    {
        var payload = new MajorEventWindowPayload
        {
            SourceSystem = "synthetic-source",
            SourceId = "EVT-3000",
            EventType = "access-window",
            Title = "Shared access window",
            Severity = "medium",
            AssetSourceId = "ASSET-3000",
            FunctionalLocationSourceId = "FL-3000",
            StartsAtUtc = ReferenceTime.AddDays(5),
            EndsAtUtc = ReferenceTime.AddDays(6),
            SourceUpdatedAtUtc = occurredAt,
            SourceDataReadiness = new SourceDataReadinessContract
            {
                Status = "Ready"
            }
        };

        return new MaintenanceEventEnvelope
        {
            EventId = eventId,
            EventType = "MajorEventWindowPublished",
            SchemaVersion = "1.0",
            SourceSystem = "synthetic-source",
            SourceRecordId = payload.SourceId,
            CorrelationId = $"corr-{eventId}",
            OccurredAt = occurredAt,
            PublishedAt = occurredAt.AddSeconds(10),
            IdempotencyKey = idempotencyKey,
            Payload = JsonSerializer.SerializeToElement(payload)
        };
    }

    private static MaintenanceEventEnvelope PartsEvent(
        string eventId,
        string idempotencyKey,
        DateTimeOffset occurredAt)
    {
        var payload = new
        {
            sourceSystem = "synthetic-source",
            sourceId = "PART-3000-WO-3000",
            workOrderSourceId = "WO-3000",
            partNumber = "KIT-3000",
            partName = "Seal kit",
            availabilityStatus = "Available",
            requiredQuantity = 1,
            availableQuantity = 1,
            neededByUtc = ReferenceTime.AddDays(5),
            sourceUpdatedAtUtc = occurredAt,
            sourceDataReadiness = new
            {
                status = "Ready",
                validationIssues = Array.Empty<ValidationIssueContract>()
            },
            validationIssues = Array.Empty<ValidationIssueContract>()
        };

        return new MaintenanceEventEnvelope
        {
            EventId = eventId,
            EventType = "PartsAvailabilityChanged",
            SchemaVersion = "1.0",
            SourceSystem = "synthetic-source",
            SourceRecordId = payload.sourceId,
            CorrelationId = $"corr-{eventId}",
            OccurredAt = occurredAt,
            PublishedAt = occurredAt.AddSeconds(10),
            IdempotencyKey = idempotencyKey,
            Payload = JsonSerializer.SerializeToElement(payload)
        };
    }

    private sealed class InMemoryImportStore : IImportStore
    {
        private readonly List<StoredImport> _imports = new();
        private readonly List<IntegrationEventAuditRecord> _events = new();
        private readonly Dictionary<string, StoredSourceRecord> _workOrders = new(StringComparer.Ordinal);
        private readonly List<MajorEventImportRecord> _majorEvents = new();

        public bool IsConfigured => true;

        public int ImportCount => _imports.Count;

        public int WorkOrderCount => _workOrders.Count;

        public int MajorEventCount => _majorEvents.Count;

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

        public Task<StaleReceivedImportSummary> CountStaleReceivedImportsAsync(
            DateTimeOffset staleBeforeUtc,
            CancellationToken cancellationToken)
        {
            var staleImports = _imports
                .Where(item => item.Status == "Received" && item.ReceivedAtUtc < staleBeforeUtc)
                .OrderBy(item => item.ReceivedAtUtc)
                .ToArray();

            return Task.FromResult(new StaleReceivedImportSummary(
                staleImports.Length,
                staleImports.FirstOrDefault()?.ReceivedAtUtc));
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

            _majorEvents.AddRange(batch.MajorEvents);
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

        public Task UpdateImportStatusAsync(
            Guid importId,
            string status,
            DateTimeOffset completedAtUtc,
            string? failureCode,
            CancellationToken cancellationToken)
        {
            var index = _imports.FindIndex(item => item.ImportId == importId);
            if (index >= 0)
            {
                _imports[index] = _imports[index] with
                {
                    Status = status,
                    CompletedAtUtc = completedAtUtc,
                    FailureCode = failureCode
                };
            }

            return Task.CompletedTask;
        }

        private static string SourceRecordKey(string sourceSystem, string sourceId)
        {
            return $"{sourceSystem}|{sourceId}";
        }
    }
}

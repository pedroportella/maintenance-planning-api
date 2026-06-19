using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaintenancePlanning.Domain.Planning;

namespace MaintenancePlanning.Application.Imports;

public sealed class ImportService(IImportStore store) : IImportService
{
    private const int StatusUnprocessableEntity = 422;
    private const int StatusConflict = 409;
    private const int StatusServiceUnavailable = 503;
    private const string ContractSchemaVersion = "1.0";
    private const string SourceWorkOrdersImportKind = "source-work-orders";
    private const string MaintenanceEventsImportKind = "maintenance-events";
    private const string StatusCompleted = "Completed";
    private const string EventStatusAccepted = "Accepted";
    private const string EventStatusRejected = "Rejected";
    private const string DispositionAccepted = "accepted";
    private const string DispositionAcceptedBlocked = "accepted-blocked";
    private const string DispositionRejected = "rejected";
    private const string DispositionIgnoredDuplicate = "ignored-duplicate";
    private const string DispositionIgnoredStale = "ignored-stale";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static readonly HashSet<string> MaintenanceEventTypes = new(StringComparer.Ordinal)
    {
        "WorkOrderCreated",
        "WorkOrderUpdated",
        "WorkOrderStatusChanged",
        "MajorEventWindowPublished",
        "PartsAvailabilityChanged",
        "CrewCapacityChanged"
    };

    private static readonly HashSet<string> WorkOrderEventTypes = new(StringComparer.Ordinal)
    {
        "WorkOrderCreated",
        "WorkOrderUpdated",
        "WorkOrderStatusChanged"
    };

    private static readonly HashSet<string> LifecycleStatuses =
        Enum.GetNames<WorkOrderLifecycleStatus>().ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> ReadinessStatuses =
        Enum.GetNames<SourceDataReadiness>().ToHashSet(StringComparer.Ordinal);

    public async Task<ImportProcessingOutcome> ImportSourceWorkOrdersAsync(
        SourceWorkOrderImportRequest request,
        CancellationToken cancellationToken)
    {
        if (!store.IsConfigured)
        {
            return StoreUnavailable();
        }

        var validation = ValidateSourceWorkOrderRequest(request);
        if (validation.Count > 0)
        {
            return ValidationFailed(validation);
        }

        var sourceSystem = Clean(request.SourceSystem);
        var idempotencyKey = Clean(request.IdempotencyKey);
        var requestHash = HashObject(request);
        var replay = await TryReplayExistingImportAsync(
            sourceSystem,
            idempotencyKey,
            requestHash,
            cancellationToken);

        if (replay is not null)
        {
            return replay;
        }

        var importedAtUtc = DateTimeOffset.UtcNow;
        var importId = Guid.NewGuid();
        var workOrders = new List<WorkOrderImportRecord>();
        var events = new List<IntegrationEventAuditRecord>();
        var latestWorkOrders = new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase);
        var acceptedCount = 0;
        var rejectedCount = 0;
        var ignoredStaleCount = 0;
        var eventResults = new List<ImportEventResult>();

        for (var index = 0; index < request.SourceWorkOrders.Count; index++)
        {
            var workOrder = request.SourceWorkOrders[index];
            var sourceUpdatedAtUtc = workOrder.SourceUpdatedAtUtc!.Value;
            var eventIdempotencyKey = BuildSyntheticEventKey(idempotencyKey, workOrder.SourceId, index);
            var eventId = $"source-work-order-{ShortHash(eventIdempotencyKey)}";
            var payloadHash = HashObject(workOrder);
            var businessIssueCode = GetBusinessIssueCode(workOrder.SourceDataReadiness, workOrder.ValidationIssues);
            var readiness = Clean(workOrder.SourceDataReadiness.Status);
            var result = new ImportEventResult(
                eventId,
                "SourceWorkOrderImported",
                Clean(workOrder.SourceId),
                eventIdempotencyKey,
                DispositionAccepted,
                EventStatusAccepted,
                readiness,
                businessIssueCode);

            if (businessIssueCode is not null && HasErrorIssue(workOrder.SourceDataReadiness, workOrder.ValidationIssues))
            {
                result = result with { Disposition = DispositionRejected, Status = EventStatusRejected };
                rejectedCount++;
                events.Add(CreateAuditEvent(importId, result, sourceSystem, ContractSchemaVersion, null, payloadHash, sourceUpdatedAtUtc, importedAtUtc, importedAtUtc));
                eventResults.Add(result);
                continue;
            }

            var latestUpdate = await GetLatestWorkOrderUpdateAsync(
                sourceSystem,
                workOrder.SourceId,
                latestWorkOrders,
                cancellationToken);

            if (latestUpdate is not null && latestUpdate.Value > sourceUpdatedAtUtc)
            {
                result = result with
                {
                    Disposition = DispositionIgnoredStale,
                    Status = EventStatusRejected
                };
                ignoredStaleCount++;
                events.Add(CreateAuditEvent(importId, result, sourceSystem, ContractSchemaVersion, null, payloadHash, sourceUpdatedAtUtc, importedAtUtc, importedAtUtc));
                eventResults.Add(result);
                continue;
            }

            if (readiness == nameof(SourceDataReadiness.Blocked))
            {
                result = result with { Disposition = DispositionAcceptedBlocked };
            }

            acceptedCount++;
            latestWorkOrders[SourceRecordKey(sourceSystem, workOrder.SourceId)] = sourceUpdatedAtUtc;
            workOrders.Add(ToWorkOrderRecord(workOrder, importedAtUtc, payloadHash));
            events.Add(CreateAuditEvent(importId, result, sourceSystem, ContractSchemaVersion, null, payloadHash, sourceUpdatedAtUtc, importedAtUtc, importedAtUtc));
            eventResults.Add(result);
        }

        var import = CreateImportAudit(
            importId,
            sourceSystem,
            SourceWorkOrdersImportKind,
            idempotencyKey,
            requestHash,
            request.SourceWorkOrders.Count,
            acceptedCount,
            rejectedCount,
            ignoredDuplicateCount: 0,
            ignoredStaleCount,
            importedAtUtc);

        await store.SaveImportAsync(
            new ImportPersistenceBatch(import, events, workOrders, Array.Empty<MajorEventImportRecord>()),
            cancellationToken);

        return ImportProcessingOutcome.Success(ToImportResult(import, duplicateRequest: false, eventResults));
    }

    public async Task<ImportProcessingOutcome> ImportMaintenanceEventsAsync(
        MaintenanceEventImportRequest request,
        CancellationToken cancellationToken)
    {
        if (!store.IsConfigured)
        {
            return StoreUnavailable();
        }

        var parsedEvents = new List<ParsedMaintenanceEvent>();
        var validation = ValidateMaintenanceEventRequest(request, parsedEvents);
        if (validation.Count > 0)
        {
            return ValidationFailed(validation);
        }

        var sourceSystem = Clean(request.SourceSystem);
        var idempotencyKey = Clean(request.BatchIdempotencyKey);
        var requestHash = HashObject(request);
        var replay = await TryReplayExistingImportAsync(
            sourceSystem,
            idempotencyKey,
            requestHash,
            cancellationToken);

        if (replay is not null)
        {
            return replay;
        }

        var importedAtUtc = DateTimeOffset.UtcNow;
        var importId = Guid.NewGuid();
        var eventResults = new List<ImportEventResult>();
        var auditEvents = new List<IntegrationEventAuditRecord>();
        var workOrders = new List<WorkOrderImportRecord>();
        var majorEvents = new List<MajorEventImportRecord>();
        var latestWorkOrders = new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase);
        var batchEventIds = new HashSet<string>(StringComparer.Ordinal);
        var batchEventIdempotencyKeys = new HashSet<string>(StringComparer.Ordinal);
        var acceptedCount = 0;
        var rejectedCount = 0;
        var ignoredDuplicateCount = 0;
        var ignoredStaleCount = 0;

        foreach (var parsedEvent in parsedEvents)
        {
            var envelope = parsedEvent.Envelope;
            var eventId = Clean(envelope.EventId);
            var eventIdempotencyKey = Clean(envelope.IdempotencyKey);
            var eventType = Clean(envelope.EventType);
            var sourceRecordId = Clean(envelope.SourceRecordId);

            if (!batchEventIds.Add(eventId)
                || !batchEventIdempotencyKeys.Add(eventIdempotencyKey)
                || await store.HasEventIdAsync(eventId, cancellationToken)
                || await store.HasEventIdempotencyKeyAsync(sourceSystem, eventIdempotencyKey, cancellationToken))
            {
                ignoredDuplicateCount++;
                eventResults.Add(new ImportEventResult(
                    eventId,
                    eventType,
                    sourceRecordId,
                    eventIdempotencyKey,
                    DispositionIgnoredDuplicate,
                    EventStatusRejected,
                    parsedEvent.ReadinessStatus,
                    parsedEvent.BusinessIssueCode));
                continue;
            }

            var payloadHash = HashJsonElement(envelope.Payload);
            var result = new ImportEventResult(
                eventId,
                eventType,
                sourceRecordId,
                eventIdempotencyKey,
                DispositionAccepted,
                EventStatusAccepted,
                parsedEvent.ReadinessStatus,
                parsedEvent.BusinessIssueCode);

            if (parsedEvent.HasErrorIssue)
            {
                result = result with { Disposition = DispositionRejected, Status = EventStatusRejected };
                rejectedCount++;
                auditEvents.Add(CreateAuditEvent(importId, result, sourceSystem, envelope.SchemaVersion, envelope.CorrelationId, payloadHash, envelope.OccurredAt!.Value, envelope.PublishedAt!.Value, importedAtUtc));
                eventResults.Add(result);
                continue;
            }

            if (parsedEvent.WorkOrder is not null)
            {
                var sourceUpdatedAtUtc = parsedEvent.WorkOrder.SourceUpdatedAtUtc!.Value;
                var latestUpdate = await GetLatestWorkOrderUpdateAsync(
                    sourceSystem,
                    parsedEvent.WorkOrder.SourceId,
                    latestWorkOrders,
                    cancellationToken);

                if (latestUpdate is not null && latestUpdate.Value > sourceUpdatedAtUtc)
                {
                    result = result with
                    {
                        Disposition = DispositionIgnoredStale,
                        Status = EventStatusRejected
                    };
                    ignoredStaleCount++;
                    auditEvents.Add(CreateAuditEvent(importId, result, sourceSystem, envelope.SchemaVersion, envelope.CorrelationId, payloadHash, envelope.OccurredAt!.Value, envelope.PublishedAt!.Value, importedAtUtc));
                    eventResults.Add(result);
                    continue;
                }

                if (parsedEvent.ReadinessStatus == nameof(SourceDataReadiness.Blocked))
                {
                    result = result with { Disposition = DispositionAcceptedBlocked };
                }

                acceptedCount++;
                latestWorkOrders[SourceRecordKey(sourceSystem, parsedEvent.WorkOrder.SourceId)] = sourceUpdatedAtUtc;
                workOrders.Add(ToWorkOrderRecord(parsedEvent.WorkOrder, importedAtUtc, payloadHash));
            }
            else if (parsedEvent.MajorEvent is not null)
            {
                acceptedCount++;
                majorEvents.Add(ToMajorEventRecord(parsedEvent.MajorEvent, importedAtUtc));
            }
            else
            {
                acceptedCount++;
            }

            auditEvents.Add(CreateAuditEvent(importId, result, sourceSystem, envelope.SchemaVersion, envelope.CorrelationId, payloadHash, envelope.OccurredAt!.Value, envelope.PublishedAt!.Value, importedAtUtc));
            eventResults.Add(result);
        }

        var import = CreateImportAudit(
            importId,
            sourceSystem,
            MaintenanceEventsImportKind,
            idempotencyKey,
            requestHash,
            request.Events.Count,
            acceptedCount,
            rejectedCount,
            ignoredDuplicateCount,
            ignoredStaleCount,
            importedAtUtc);

        await store.SaveImportAsync(
            new ImportPersistenceBatch(import, auditEvents, workOrders, majorEvents),
            cancellationToken);

        return ImportProcessingOutcome.Success(ToImportResult(import, duplicateRequest: false, eventResults));
    }

    private async Task<ImportProcessingOutcome?> TryReplayExistingImportAsync(
        string sourceSystem,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await store.FindImportAsync(sourceSystem, idempotencyKey, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return ImportProcessingOutcome.Failed(new ImportProblem(
                StatusConflict,
                "Import idempotency conflict.",
                "The idempotency key has already been used with a different request body.",
                "idempotency-conflict"));
        }

        var result = new ImportResult(
            existing.ImportId,
            existing.SourceSystem,
            existing.ImportKind,
            existing.IdempotencyKey,
            existing.Status,
            existing.ReceivedCount,
            existing.AcceptedCount,
            existing.RejectedCount,
            existing.IgnoredDuplicateCount,
            existing.IgnoredStaleCount,
            DuplicateRequest: true,
            existing.ReceivedAtUtc,
            existing.CompletedAtUtc,
            existing.Events);

        return ImportProcessingOutcome.Success(result);
    }

    private async Task<DateTimeOffset?> GetLatestWorkOrderUpdateAsync(
        string sourceSystem,
        string sourceId,
        Dictionary<string, DateTimeOffset?> latestWorkOrders,
        CancellationToken cancellationToken)
    {
        var key = SourceRecordKey(sourceSystem, sourceId);
        if (latestWorkOrders.TryGetValue(key, out var latest))
        {
            return latest;
        }

        var stored = await store.FindWorkOrderAsync(sourceSystem, sourceId, cancellationToken);
        latest = stored?.SourceUpdatedAtUtc;
        latestWorkOrders[key] = latest;
        return latest;
    }

    private static Dictionary<string, List<string>> ValidateSourceWorkOrderRequest(SourceWorkOrderImportRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        RequireString(errors, "sourceSystem", request.SourceSystem, 80);
        RequireConst(errors, "schemaVersion", request.SchemaVersion, ContractSchemaVersion);
        RequireString(errors, "idempotencyKey", request.IdempotencyKey, 160);

        if (request.SourceWorkOrders.Count == 0)
        {
            AddError(errors, "sourceWorkOrders", "At least one source work order is required.");
        }

        for (var index = 0; index < request.SourceWorkOrders.Count; index++)
        {
            ValidateWorkOrderPayload(errors, $"sourceWorkOrders[{index}]", request.SourceWorkOrders[index], request.SourceSystem);
        }

        return errors;
    }

    private static Dictionary<string, List<string>> ValidateMaintenanceEventRequest(
        MaintenanceEventImportRequest request,
        List<ParsedMaintenanceEvent> parsedEvents)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        RequireString(errors, "sourceSystem", request.SourceSystem, 80);
        RequireConst(errors, "schemaVersion", request.SchemaVersion, ContractSchemaVersion);
        RequireString(errors, "batchIdempotencyKey", request.BatchIdempotencyKey, 160);

        if (request.Events.Count == 0)
        {
            AddError(errors, "events", "At least one maintenance event is required.");
            return errors;
        }

        for (var index = 0; index < request.Events.Count; index++)
        {
            var path = $"events[{index}]";
            var envelope = request.Events[index];
            ValidateEnvelope(errors, path, envelope, request.SourceSystem);

            if (errors.Count > 0 && !MaintenanceEventTypes.Contains(envelope.EventType))
            {
                continue;
            }

            parsedEvents.Add(ParsePayload(errors, path, envelope, request.SourceSystem));
        }

        if (errors.Count > 0)
        {
            parsedEvents.Clear();
        }

        return errors;
    }

    private static void ValidateEnvelope(
        Dictionary<string, List<string>> errors,
        string path,
        MaintenanceEventEnvelope envelope,
        string requestSourceSystem)
    {
        RequireString(errors, $"{path}.eventId", envelope.EventId, 160);
        RequireEnum(errors, $"{path}.eventType", envelope.EventType, MaintenanceEventTypes);
        RequireConst(errors, $"{path}.schemaVersion", envelope.SchemaVersion, ContractSchemaVersion);
        RequireString(errors, $"{path}.sourceSystem", envelope.SourceSystem, 80);
        RequireString(errors, $"{path}.sourceRecordId", envelope.SourceRecordId, 120);
        RequireString(errors, $"{path}.correlationId", envelope.CorrelationId, 160);
        RequireString(errors, $"{path}.idempotencyKey", envelope.IdempotencyKey, 160);
        RequireDate(errors, $"{path}.occurredAt", envelope.OccurredAt);
        RequireDate(errors, $"{path}.publishedAt", envelope.PublishedAt);

        if (!string.IsNullOrWhiteSpace(envelope.SourceSystem)
            && !string.Equals(Clean(envelope.SourceSystem), Clean(requestSourceSystem), StringComparison.Ordinal))
        {
            AddError(errors, $"{path}.sourceSystem", "Must match the import sourceSystem.");
        }

        if (envelope.PublishedAt is not null
            && envelope.OccurredAt is not null
            && envelope.PublishedAt.Value < envelope.OccurredAt.Value)
        {
            AddError(errors, $"{path}.publishedAt", "Must not be earlier than occurredAt.");
        }

        if (envelope.Payload.ValueKind is not JsonValueKind.Object)
        {
            AddError(errors, $"{path}.payload", "Payload must be an object.");
        }
    }

    private static ParsedMaintenanceEvent ParsePayload(
        Dictionary<string, List<string>> errors,
        string path,
        MaintenanceEventEnvelope envelope,
        string requestSourceSystem)
    {
        if (envelope.Payload.ValueKind is not JsonValueKind.Object)
        {
            return ParsedMaintenanceEvent.Empty(envelope);
        }

        if (WorkOrderEventTypes.Contains(envelope.EventType))
        {
            var workOrder = DeserializePayload<SourceWorkOrderPayload>(errors, $"{path}.payload", envelope.Payload);
            if (workOrder is not null)
            {
                ValidateWorkOrderPayload(errors, $"{path}.payload", workOrder, requestSourceSystem);
                ValidatePayloadEnvelopeAlignment(errors, path, envelope, workOrder.SourceSystem, workOrder.SourceId);
                return ParsedMaintenanceEvent.FromWorkOrder(envelope, workOrder);
            }
        }
        else if (envelope.EventType == "MajorEventWindowPublished")
        {
            var majorEvent = DeserializePayload<MajorEventWindowPayload>(errors, $"{path}.payload", envelope.Payload);
            if (majorEvent is not null)
            {
                ValidateMajorEventPayload(errors, $"{path}.payload", majorEvent, requestSourceSystem);
                ValidatePayloadEnvelopeAlignment(errors, path, envelope, majorEvent.SourceSystem, majorEvent.SourceId);
                return ParsedMaintenanceEvent.FromMajorEvent(envelope, majorEvent);
            }
        }
        else
        {
            var genericPayload = DeserializePayload<GenericEventPayload>(errors, $"{path}.payload", envelope.Payload);
            if (genericPayload is not null)
            {
                ValidateGenericPayload(errors, $"{path}.payload", genericPayload, requestSourceSystem);
                ValidatePayloadEnvelopeAlignment(errors, path, envelope, genericPayload.SourceSystem, genericPayload.SourceId);
                return ParsedMaintenanceEvent.FromGeneric(envelope, genericPayload);
            }
        }

        return ParsedMaintenanceEvent.Empty(envelope);
    }

    private static T? DeserializePayload<T>(
        Dictionary<string, List<string>> errors,
        string path,
        JsonElement payload)
    {
        try
        {
            var value = payload.Deserialize<T>(JsonOptions);
            if (value is null)
            {
                AddError(errors, path, "Payload could not be read.");
            }

            return value;
        }
        catch (JsonException)
        {
            AddError(errors, path, "Payload could not be read.");
            return default;
        }
    }

    private static void ValidatePayloadEnvelopeAlignment(
        Dictionary<string, List<string>> errors,
        string path,
        MaintenanceEventEnvelope envelope,
        string payloadSourceSystem,
        string payloadSourceId)
    {
        if (!string.Equals(Clean(envelope.SourceSystem), Clean(payloadSourceSystem), StringComparison.Ordinal))
        {
            AddError(errors, $"{path}.payload.sourceSystem", "Must match the envelope sourceSystem.");
        }

        if (!string.Equals(Clean(envelope.SourceRecordId), Clean(payloadSourceId), StringComparison.Ordinal))
        {
            AddError(errors, $"{path}.payload.sourceId", "Must match the envelope sourceRecordId.");
        }
    }

    private static void ValidateWorkOrderPayload(
        Dictionary<string, List<string>> errors,
        string path,
        SourceWorkOrderPayload payload,
        string requestSourceSystem)
    {
        RequireString(errors, $"{path}.sourceSystem", payload.SourceSystem, 80);
        RequireString(errors, $"{path}.sourceId", payload.SourceId, 120);
        RequireString(errors, $"{path}.workOrderNumber", payload.WorkOrderNumber, 120);
        RequireString(errors, $"{path}.title", payload.Title, 240);
        RequireString(errors, $"{path}.workType", payload.WorkType, 80);
        RequireString(errors, $"{path}.priority", payload.Priority, 40);
        RequireEnum(errors, $"{path}.lifecycleStatus", payload.LifecycleStatus, LifecycleStatuses);
        OptionalString(errors, $"{path}.assetSourceId", payload.AssetSourceId, 120);
        OptionalString(errors, $"{path}.functionalLocationSourceId", payload.FunctionalLocationSourceId, 120);
        RequireDate(errors, $"{path}.sourceUpdatedAtUtc", payload.SourceUpdatedAtUtc);
        ValidateReadiness(errors, $"{path}.sourceDataReadiness", payload.SourceDataReadiness);
        ValidateValidationIssues(errors, $"{path}.validationIssues", payload.ValidationIssues);

        if (!string.IsNullOrWhiteSpace(payload.SourceSystem)
            && !string.Equals(Clean(payload.SourceSystem), Clean(requestSourceSystem), StringComparison.Ordinal))
        {
            AddError(errors, $"{path}.sourceSystem", "Must match the import sourceSystem.");
        }

        if (payload.EstimatedHours is < 0)
        {
            AddError(errors, $"{path}.estimatedHours", "Must be greater than or equal to zero.");
        }

        if (payload.RequiredStartUtc is not null
            && payload.DueAtUtc is not null
            && payload.DueAtUtc.Value < payload.RequiredStartUtc.Value)
        {
            AddError(errors, $"{path}.dueAtUtc", "Must not be earlier than requiredStartUtc.");
        }
    }

    private static void ValidateMajorEventPayload(
        Dictionary<string, List<string>> errors,
        string path,
        MajorEventWindowPayload payload,
        string requestSourceSystem)
    {
        RequireString(errors, $"{path}.sourceSystem", payload.SourceSystem, 80);
        RequireString(errors, $"{path}.sourceId", payload.SourceId, 120);
        RequireString(errors, $"{path}.eventType", payload.EventType, 80);
        RequireString(errors, $"{path}.title", payload.Title, 240);
        RequireString(errors, $"{path}.severity", payload.Severity, 40);
        OptionalString(errors, $"{path}.assetSourceId", payload.AssetSourceId, 120);
        OptionalString(errors, $"{path}.functionalLocationSourceId", payload.FunctionalLocationSourceId, 120);
        RequireDate(errors, $"{path}.startsAtUtc", payload.StartsAtUtc);
        RequireDate(errors, $"{path}.sourceUpdatedAtUtc", payload.SourceUpdatedAtUtc);
        ValidateReadiness(errors, $"{path}.sourceDataReadiness", payload.SourceDataReadiness);
        ValidateValidationIssues(errors, $"{path}.validationIssues", payload.ValidationIssues);

        if (!string.IsNullOrWhiteSpace(payload.SourceSystem)
            && !string.Equals(Clean(payload.SourceSystem), Clean(requestSourceSystem), StringComparison.Ordinal))
        {
            AddError(errors, $"{path}.sourceSystem", "Must match the import sourceSystem.");
        }

        if (payload.StartsAtUtc is not null
            && payload.EndsAtUtc is not null
            && payload.EndsAtUtc.Value <= payload.StartsAtUtc.Value)
        {
            AddError(errors, $"{path}.endsAtUtc", "Must be later than startsAtUtc.");
        }
    }

    private static void ValidateGenericPayload(
        Dictionary<string, List<string>> errors,
        string path,
        GenericEventPayload payload,
        string requestSourceSystem)
    {
        RequireString(errors, $"{path}.sourceSystem", payload.SourceSystem, 80);
        RequireString(errors, $"{path}.sourceId", payload.SourceId, 120);
        RequireDate(errors, $"{path}.sourceUpdatedAtUtc", payload.SourceUpdatedAtUtc);
        ValidateReadiness(errors, $"{path}.sourceDataReadiness", payload.SourceDataReadiness);
        ValidateValidationIssues(errors, $"{path}.validationIssues", payload.ValidationIssues);

        if (!string.IsNullOrWhiteSpace(payload.SourceSystem)
            && !string.Equals(Clean(payload.SourceSystem), Clean(requestSourceSystem), StringComparison.Ordinal))
        {
            AddError(errors, $"{path}.sourceSystem", "Must match the import sourceSystem.");
        }
    }

    private static void ValidateReadiness(
        Dictionary<string, List<string>> errors,
        string path,
        SourceDataReadinessContract readiness)
    {
        RequireEnum(errors, $"{path}.status", readiness.Status, ReadinessStatuses);
        OptionalString(errors, $"{path}.issueCode", readiness.IssueCode, 80);
        OptionalString(errors, $"{path}.issueDetail", readiness.IssueDetail, 500);
        ValidateValidationIssues(errors, $"{path}.validationIssues", readiness.ValidationIssues);
    }

    private static void ValidateValidationIssues(
        Dictionary<string, List<string>> errors,
        string path,
        IReadOnlyList<ValidationIssueContract> issues)
    {
        for (var index = 0; index < issues.Count; index++)
        {
            var issuePath = $"{path}[{index}]";
            RequireString(errors, $"{issuePath}.code", issues[index].Code, 80);
            RequireEnum(errors, $"{issuePath}.severity", issues[index].Severity, new HashSet<string>(StringComparer.Ordinal)
            {
                "info",
                "warning",
                "error"
            });
            OptionalString(errors, $"{issuePath}.sourceField", issues[index].SourceField, 120);
            OptionalString(errors, $"{issuePath}.detail", issues[index].Detail, 500);
        }
    }

    private static IntegrationEventAuditRecord CreateAuditEvent(
        Guid importId,
        ImportEventResult result,
        string sourceSystem,
        string schemaVersion,
        string? correlationId,
        string payloadHash,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset publishedAtUtc,
        DateTimeOffset recordedAtUtc)
    {
        return new IntegrationEventAuditRecord(
            Guid.NewGuid(),
            importId,
            result.EventId,
            result.EventType,
            Clean(schemaVersion),
            sourceSystem,
            result.SourceRecordId,
            Clean(correlationId ?? $"import-{importId:N}"),
            result.IdempotencyKey,
            result.Disposition,
            result.Status,
            IsWorkOrderEvent(result.EventType) || result.EventType == "SourceWorkOrderImported" ? result.SourceRecordId : null,
            payloadHash,
            occurredAtUtc,
            publishedAtUtc,
            recordedAtUtc,
            result.Readiness,
            result.ValidationIssueCode);
    }

    private static ImportAuditRecord CreateImportAudit(
        Guid importId,
        string sourceSystem,
        string importKind,
        string idempotencyKey,
        string requestHash,
        int receivedCount,
        int acceptedCount,
        int rejectedCount,
        int ignoredDuplicateCount,
        int ignoredStaleCount,
        DateTimeOffset importedAtUtc)
    {
        return new ImportAuditRecord(
            importId,
            sourceSystem,
            importKind,
            idempotencyKey,
            requestHash,
            StatusCompleted,
            receivedCount,
            acceptedCount,
            rejectedCount,
            ignoredDuplicateCount,
            ignoredStaleCount,
            importedAtUtc,
            importedAtUtc,
            FailureCode: null);
    }

    private static WorkOrderImportRecord ToWorkOrderRecord(
        SourceWorkOrderPayload payload,
        DateTimeOffset importedAtUtc,
        string payloadHash)
    {
        return new WorkOrderImportRecord(
            Clean(payload.SourceSystem),
            Clean(payload.SourceId),
            Clean(payload.WorkOrderNumber),
            Clean(payload.Title),
            Clean(payload.WorkType),
            Clean(payload.Priority),
            Clean(payload.LifecycleStatus),
            Clean(payload.SourceDataReadiness.Status),
            CleanOptional(payload.SourceDataReadiness.IssueCode),
            CleanOptional(payload.SourceDataReadiness.IssueDetail),
            CleanOptional(payload.AssetSourceId),
            CleanOptional(payload.FunctionalLocationSourceId),
            payload.RequiredStartUtc,
            payload.DueAtUtc,
            payload.ScheduledStartUtc,
            payload.EstimatedHours,
            payload.SourceUpdatedAtUtc!.Value,
            importedAtUtc,
            payloadHash);
    }

    private static MajorEventImportRecord ToMajorEventRecord(
        MajorEventWindowPayload payload,
        DateTimeOffset importedAtUtc)
    {
        return new MajorEventImportRecord(
            Clean(payload.SourceSystem),
            Clean(payload.SourceId),
            Clean(payload.EventType),
            Clean(payload.Title),
            Clean(payload.Severity),
            CleanOptional(payload.SourceDataReadiness.IssueCode),
            CleanOptional(payload.AssetSourceId),
            CleanOptional(payload.FunctionalLocationSourceId),
            payload.StartsAtUtc!.Value,
            payload.EndsAtUtc,
            importedAtUtc);
    }

    private static ImportResult ToImportResult(
        ImportAuditRecord import,
        bool duplicateRequest,
        IReadOnlyList<ImportEventResult> events)
    {
        return new ImportResult(
            import.Id,
            import.SourceSystem,
            import.ImportKind,
            import.IdempotencyKey,
            import.Status,
            import.ReceivedCount,
            import.AcceptedCount,
            import.RejectedCount,
            import.IgnoredDuplicateCount,
            import.IgnoredStaleCount,
            duplicateRequest,
            import.ReceivedAtUtc,
            import.CompletedAtUtc,
            events);
    }

    private static ImportProcessingOutcome StoreUnavailable()
    {
        return ImportProcessingOutcome.Failed(new ImportProblem(
            StatusServiceUnavailable,
            "Import persistence is not configured.",
            "Configure the local database before using import endpoints.",
            "import-persistence-not-configured"));
    }

    private static ImportProcessingOutcome ValidationFailed(Dictionary<string, List<string>> errors)
    {
        return ImportProcessingOutcome.Failed(new ImportProblem(
            StatusUnprocessableEntity,
            "Import request validation failed.",
            "One or more import fields are invalid.",
            "import-validation-failed",
            errors.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.Ordinal)));
    }

    private static string? GetBusinessIssueCode(
        SourceDataReadinessContract readiness,
        IReadOnlyList<ValidationIssueContract> payloadIssues)
    {
        if (!string.IsNullOrWhiteSpace(readiness.IssueCode))
        {
            return Clean(readiness.IssueCode);
        }

        return readiness.ValidationIssues.Concat(payloadIssues).FirstOrDefault(issue => !string.IsNullOrWhiteSpace(issue.Code))?.Code;
    }

    private static string? GetBusinessIssueCode(GenericEventPayload payload)
    {
        return GetBusinessIssueCode(payload.SourceDataReadiness, payload.ValidationIssues);
    }

    private static bool HasErrorIssue(
        SourceDataReadinessContract readiness,
        IReadOnlyList<ValidationIssueContract> payloadIssues)
    {
        return readiness.ValidationIssues.Concat(payloadIssues).Any(issue => string.Equals(issue.Severity, "error", StringComparison.Ordinal));
    }

    private static bool HasErrorIssue(GenericEventPayload payload)
    {
        return HasErrorIssue(payload.SourceDataReadiness, payload.ValidationIssues);
    }

    private static bool IsWorkOrderEvent(string eventType)
    {
        return WorkOrderEventTypes.Contains(eventType);
    }

    private static void RequireString(
        Dictionary<string, List<string>> errors,
        string path,
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, path, "Field is required.");
            return;
        }

        if (Clean(value).Length > maxLength)
        {
            AddError(errors, path, $"Must be {maxLength.ToString(CultureInfo.InvariantCulture)} characters or fewer.");
        }
    }

    private static void RequireConst(
        Dictionary<string, List<string>> errors,
        string path,
        string? value,
        string expected)
    {
        RequireString(errors, path, value, expected.Length);

        if (!string.IsNullOrWhiteSpace(value) && !string.Equals(Clean(value), expected, StringComparison.Ordinal))
        {
            AddError(errors, path, $"Must be {expected}.");
        }
    }

    private static void RequireEnum(
        Dictionary<string, List<string>> errors,
        string path,
        string? value,
        IReadOnlySet<string> allowedValues)
    {
        RequireString(errors, path, value, 160);

        if (!string.IsNullOrWhiteSpace(value) && !allowedValues.Contains(Clean(value)))
        {
            AddError(errors, path, "Contains an unsupported value.");
        }
    }

    private static void OptionalString(
        Dictionary<string, List<string>> errors,
        string path,
        string? value,
        int maxLength)
    {
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, path, "Must be null or contain text.");
            return;
        }

        if (Clean(value).Length > maxLength)
        {
            AddError(errors, path, $"Must be {maxLength.ToString(CultureInfo.InvariantCulture)} characters or fewer.");
        }
    }

    private static void RequireDate(
        Dictionary<string, List<string>> errors,
        string path,
        DateTimeOffset? value)
    {
        if (value is null)
        {
            AddError(errors, path, "Field is required.");
        }
    }

    private static void AddError(
        Dictionary<string, List<string>> errors,
        string path,
        string message)
    {
        if (!errors.TryGetValue(path, out var messages))
        {
            messages = new List<string>();
            errors[path] = messages;
        }

        messages.Add(message);
    }

    private static string HashObject<T>(T value)
    {
        return HashBytes(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));
    }

    private static string HashJsonElement(JsonElement value)
    {
        return HashBytes(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));
    }

    private static string HashBytes(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string ShortHash(string value)
    {
        var hash = HashBytes(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));
        return hash[..24];
    }

    private static string BuildSyntheticEventKey(string importIdempotencyKey, string sourceId, int index)
    {
        var raw = $"{importIdempotencyKey}:source-work-order:{sourceId}:{index.ToString(CultureInfo.InvariantCulture)}";
        return raw.Length <= 160 ? raw : $"source-work-order:{ShortHash(raw)}";
    }

    private static string SourceRecordKey(string sourceSystem, string sourceId)
    {
        return $"{Clean(sourceSystem)}|{Clean(sourceId)}";
    }

    private static string Clean(string value)
    {
        return value.Trim();
    }

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class GenericEventPayload
    {
        public string SourceSystem { get; init; } = "";

        public string SourceId { get; init; } = "";

        public DateTimeOffset? SourceUpdatedAtUtc { get; init; }

        public SourceDataReadinessContract SourceDataReadiness { get; init; } = new();

        public IReadOnlyList<ValidationIssueContract> ValidationIssues { get; init; } = Array.Empty<ValidationIssueContract>();
    }

    private sealed record ParsedMaintenanceEvent(
        MaintenanceEventEnvelope Envelope,
        SourceWorkOrderPayload? WorkOrder,
        MajorEventWindowPayload? MajorEvent,
        GenericEventPayload? GenericPayload)
    {
        public string? ReadinessStatus =>
            WorkOrder?.SourceDataReadiness.Status
            ?? MajorEvent?.SourceDataReadiness.Status
            ?? GenericPayload?.SourceDataReadiness.Status;

        public string? BusinessIssueCode =>
            WorkOrder is not null
                ? GetBusinessIssueCode(WorkOrder.SourceDataReadiness, WorkOrder.ValidationIssues)
                : MajorEvent is not null
                    ? GetBusinessIssueCode(MajorEvent.SourceDataReadiness, MajorEvent.ValidationIssues)
                    : GenericPayload is not null
                        ? GetBusinessIssueCode(GenericPayload)
                        : null;

        public bool HasErrorIssue =>
            WorkOrder is not null
                ? ImportService.HasErrorIssue(WorkOrder.SourceDataReadiness, WorkOrder.ValidationIssues)
                : MajorEvent is not null
                    ? ImportService.HasErrorIssue(MajorEvent.SourceDataReadiness, MajorEvent.ValidationIssues)
                    : GenericPayload is not null && ImportService.HasErrorIssue(GenericPayload);

        public static ParsedMaintenanceEvent FromWorkOrder(
            MaintenanceEventEnvelope envelope,
            SourceWorkOrderPayload workOrder) =>
            new(envelope, workOrder, null, null);

        public static ParsedMaintenanceEvent FromMajorEvent(
            MaintenanceEventEnvelope envelope,
            MajorEventWindowPayload majorEvent) =>
            new(envelope, null, majorEvent, null);

        public static ParsedMaintenanceEvent FromGeneric(
            MaintenanceEventEnvelope envelope,
            GenericEventPayload genericPayload) =>
            new(envelope, null, null, genericPayload);

        public static ParsedMaintenanceEvent Empty(MaintenanceEventEnvelope envelope) =>
            new(envelope, null, null, null);
    }
}

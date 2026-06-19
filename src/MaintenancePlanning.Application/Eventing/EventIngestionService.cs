using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaintenancePlanning.Application.Imports;

namespace MaintenancePlanning.Application.Eventing;

public sealed class EventIngestionService(
    IImportService importService,
    IImportStore importStore) : IEventIngestionService
{
    private const string FailedImportKind = "event-ingestion";
    private const string FailedStatus = "Failed";
    private const string UnknownSourceSystem = "unknown-source";
    private const string InvalidMessageCode = "event-message-invalid";
    private const string PersistenceUnavailableCode = "import-persistence-not-configured";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public async Task<EventIngestionResult> ProcessAsync(
        QueuedEventMessage message,
        CancellationToken cancellationToken)
    {
        if (!importStore.IsConfigured)
        {
            return EventIngestionResult.Retry(message.MessageId, PersistenceUnavailableCode);
        }

        ParsedQueueEvent parsed;
        try
        {
            parsed = Parse(message);
        }
        catch (JsonException)
        {
            await SaveFailedImportAsync(
                UnknownSourceSystem,
                BuildMessageFailureKey(message),
                HashText(message.Body),
                InvalidMessageCode,
                cancellationToken);

            return EventIngestionResult.Failed(message.MessageId, InvalidMessageCode);
        }

        var outcome = await importService.ImportMaintenanceEventsAsync(parsed.Request, cancellationToken);
        if (outcome.Result is not null)
        {
            return EventIngestionResult.Processed(message.MessageId, outcome.Result);
        }

        var problem = outcome.Problem ?? new ImportProblem(
            StatusCode: 422,
            Title: "Event ingestion failed.",
            Detail: "The queued event could not be imported.",
            Code: InvalidMessageCode);

        if (problem.Code == PersistenceUnavailableCode || problem.StatusCode == 503)
        {
            return EventIngestionResult.Retry(message.MessageId, problem.Code);
        }

        if (problem.StatusCode != 409)
        {
            await SaveFailedImportAsync(
                parsed.SourceSystem,
                parsed.BatchIdempotencyKey,
                parsed.RequestHash,
                problem.Code,
                cancellationToken);
        }

        return EventIngestionResult.Failed(message.MessageId, problem.Code);
    }

    private async Task SaveFailedImportAsync(
        string sourceSystem,
        string idempotencyKey,
        string requestHash,
        string failureCode,
        CancellationToken cancellationToken)
    {
        var existing = await importStore.FindImportAsync(sourceSystem, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var recordedAtUtc = DateTimeOffset.UtcNow;
        var import = new ImportAuditRecord(
            Guid.NewGuid(),
            sourceSystem,
            FailedImportKind,
            idempotencyKey,
            requestHash,
            FailedStatus,
            ReceivedCount: 1,
            AcceptedCount: 0,
            RejectedCount: 1,
            IgnoredDuplicateCount: 0,
            IgnoredStaleCount: 0,
            recordedAtUtc,
            recordedAtUtc,
            CleanFailureCode(failureCode));

        await importStore.SaveImportAsync(
            new ImportPersistenceBatch(
                import,
                Array.Empty<IntegrationEventAuditRecord>(),
                Array.Empty<WorkOrderImportRecord>(),
                Array.Empty<MajorEventImportRecord>()),
            cancellationToken);
    }

    private static ParsedQueueEvent Parse(QueuedEventMessage message)
    {
        using var document = JsonDocument.Parse(message.Body);
        var root = document.RootElement;
        var eventBridgeId = GetOptionalString(root, "id");
        var envelopeElement = root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.Object
            ? detail
            : root;
        var envelope = envelopeElement.Deserialize<MaintenanceEventEnvelope>(JsonOptions)
            ?? throw new JsonException("Queued event could not be deserialized.");

        var sourceSystem = string.IsNullOrWhiteSpace(envelope.SourceSystem)
            ? UnknownSourceSystem
            : envelope.SourceSystem.Trim();
        var schemaVersion = envelope.SchemaVersion;
        var batchIdempotencyKey = BuildBatchIdempotencyKey(eventBridgeId, envelope, message);
        var request = new MaintenanceEventImportRequest
        {
            SourceSystem = sourceSystem,
            SchemaVersion = schemaVersion,
            BatchIdempotencyKey = batchIdempotencyKey,
            Events = new[] { envelope }
        };

        return new ParsedQueueEvent(sourceSystem, batchIdempotencyKey, HashText(message.Body), request);
    }

    private static string BuildBatchIdempotencyKey(
        string? eventBridgeId,
        MaintenanceEventEnvelope envelope,
        QueuedEventMessage message)
    {
        var raw = !string.IsNullOrWhiteSpace(eventBridgeId)
            ? $"eventbridge:{eventBridgeId.Trim()}"
            : !string.IsNullOrWhiteSpace(envelope.EventId)
                ? $"event:{envelope.EventId.Trim()}"
                : BuildMessageFailureKey(message);

        return raw.Length <= 160 ? raw : $"event:{ShortHash(raw)}";
    }

    private static string BuildMessageFailureKey(QueuedEventMessage message)
    {
        var raw = string.IsNullOrWhiteSpace(message.MessageId)
            ? $"message:{ShortHash(message.Body)}"
            : $"message:{message.MessageId.Trim()}";

        return raw.Length <= 160 ? raw : $"message:{ShortHash(raw)}";
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string HashText(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string ShortHash(string value)
    {
        return HashText(value)[..24];
    }

    private static string CleanFailureCode(string failureCode)
    {
        var clean = string.IsNullOrWhiteSpace(failureCode) ? InvalidMessageCode : failureCode.Trim();
        return clean.Length <= 80 ? clean : clean[..80];
    }

    private sealed record ParsedQueueEvent(
        string SourceSystem,
        string BatchIdempotencyKey,
        string RequestHash,
        MaintenanceEventImportRequest Request);
}

using System.Text.Json;

namespace MaintenancePlanning.Application.Imports;

public sealed class SourceWorkOrderImportRequest
{
    public string SourceSystem { get; init; } = "";

    public string SchemaVersion { get; init; } = "";

    public string IdempotencyKey { get; init; } = "";

    public IReadOnlyList<SourceWorkOrderPayload> SourceWorkOrders { get; init; } = Array.Empty<SourceWorkOrderPayload>();
}

public sealed class MaintenanceEventImportRequest
{
    public string SourceSystem { get; init; } = "";

    public string SchemaVersion { get; init; } = "";

    public string BatchIdempotencyKey { get; init; } = "";

    public IReadOnlyList<MaintenanceEventEnvelope> Events { get; init; } = Array.Empty<MaintenanceEventEnvelope>();
}

public sealed class MaintenanceEventEnvelope
{
    public string EventId { get; init; } = "";

    public string EventType { get; init; } = "";

    public string SchemaVersion { get; init; } = "";

    public string SourceSystem { get; init; } = "";

    public string SourceRecordId { get; init; } = "";

    public string CorrelationId { get; init; } = "";

    public DateTimeOffset? OccurredAt { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public string IdempotencyKey { get; init; } = "";

    public JsonElement Payload { get; init; }
}

public sealed class SourceWorkOrderPayload
{
    public string SourceSystem { get; init; } = "";

    public string SourceId { get; init; } = "";

    public string WorkOrderNumber { get; init; } = "";

    public string Title { get; init; } = "";

    public string WorkType { get; init; } = "";

    public string Priority { get; init; } = "";

    public string LifecycleStatus { get; init; } = "";

    public string? AssetSourceId { get; init; }

    public string? FunctionalLocationSourceId { get; init; }

    public DateTimeOffset? RequiredStartUtc { get; init; }

    public DateTimeOffset? DueAtUtc { get; init; }

    public DateTimeOffset? ScheduledStartUtc { get; init; }

    public decimal? EstimatedHours { get; init; }

    public DateTimeOffset? SourceUpdatedAtUtc { get; init; }

    public SourceDataReadinessContract SourceDataReadiness { get; init; } = new();

    public IReadOnlyList<ValidationIssueContract> ValidationIssues { get; init; } = Array.Empty<ValidationIssueContract>();
}

public sealed class MajorEventWindowPayload
{
    public string SourceSystem { get; init; } = "";

    public string SourceId { get; init; } = "";

    public string EventType { get; init; } = "";

    public string Title { get; init; } = "";

    public string Severity { get; init; } = "";

    public string? AssetSourceId { get; init; }

    public string? FunctionalLocationSourceId { get; init; }

    public DateTimeOffset? StartsAtUtc { get; init; }

    public DateTimeOffset? EndsAtUtc { get; init; }

    public DateTimeOffset? SourceUpdatedAtUtc { get; init; }

    public SourceDataReadinessContract SourceDataReadiness { get; init; } = new();

    public IReadOnlyList<ValidationIssueContract> ValidationIssues { get; init; } = Array.Empty<ValidationIssueContract>();
}

public sealed class SourceDataReadinessContract
{
    public string Status { get; init; } = "";

    public string? IssueCode { get; init; }

    public string? IssueDetail { get; init; }

    public IReadOnlyList<ValidationIssueContract> ValidationIssues { get; init; } = Array.Empty<ValidationIssueContract>();
}

public sealed class ValidationIssueContract
{
    public string Code { get; init; } = "";

    public string Severity { get; init; } = "";

    public string? SourceField { get; init; }

    public string? Detail { get; init; }
}

public sealed record ImportResult(
    Guid ImportId,
    string SourceSystem,
    string ImportKind,
    string IdempotencyKey,
    string Status,
    int ReceivedCount,
    int AcceptedCount,
    int RejectedCount,
    int IgnoredDuplicateCount,
    int IgnoredStaleCount,
    bool DuplicateRequest,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<ImportEventResult> Events);

public sealed record ImportEventResult(
    string EventId,
    string EventType,
    string SourceRecordId,
    string IdempotencyKey,
    string Disposition,
    string Status,
    string? Readiness,
    string? ValidationIssueCode);

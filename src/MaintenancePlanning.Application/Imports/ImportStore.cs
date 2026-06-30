namespace MaintenancePlanning.Application.Imports;

public interface IImportStore
{
    bool IsConfigured { get; }

    Task<StoredImport?> FindImportAsync(
        string sourceSystem,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<StoredImport?> FindLatestImportAsync(CancellationToken cancellationToken);

    Task<StoredImport?> FindLatestFailedImportAsync(CancellationToken cancellationToken);

    Task<StaleReceivedImportSummary> CountStaleReceivedImportsAsync(
        DateTimeOffset staleBeforeUtc,
        CancellationToken cancellationToken);

    Task<bool> HasEventIdempotencyKeyAsync(
        string sourceSystem,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<bool> HasEventIdAsync(
        string eventId,
        CancellationToken cancellationToken);

    Task<StoredSourceRecord?> FindWorkOrderAsync(
        string sourceSystem,
        string sourceId,
        CancellationToken cancellationToken);

    Task SaveImportAsync(
        ImportPersistenceBatch batch,
        CancellationToken cancellationToken);

    Task UpdateImportStatusAsync(
        Guid importId,
        string status,
        DateTimeOffset completedAtUtc,
        string? failureCode,
        CancellationToken cancellationToken);
}

public sealed record StoredImport(
    Guid ImportId,
    string SourceSystem,
    string ImportKind,
    string IdempotencyKey,
    string RequestHash,
    string Status,
    int ReceivedCount,
    int AcceptedCount,
    int RejectedCount,
    int IgnoredDuplicateCount,
    int IgnoredStaleCount,
    string? FailureCode,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<ImportEventResult> Events);

public sealed record StaleReceivedImportSummary(
    int Count,
    DateTimeOffset? OldestReceivedAtUtc);

public sealed record StoredSourceRecord(
    string SourceSystem,
    string SourceId,
    DateTimeOffset SourceUpdatedAtUtc);

public sealed record ImportPersistenceBatch(
    ImportAuditRecord Import,
    IReadOnlyList<IntegrationEventAuditRecord> Events,
    IReadOnlyList<WorkOrderImportRecord> WorkOrders,
    IReadOnlyList<MajorEventImportRecord> MajorEvents);

public sealed record ImportAuditRecord(
    Guid Id,
    string SourceSystem,
    string ImportKind,
    string IdempotencyKey,
    string RequestHash,
    string Status,
    int ReceivedCount,
    int AcceptedCount,
    int RejectedCount,
    int IgnoredDuplicateCount,
    int IgnoredStaleCount,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? FailureCode);

public sealed record IntegrationEventAuditRecord(
    Guid Id,
    Guid IntegrationImportId,
    string EventId,
    string EventType,
    string SchemaVersion,
    string SourceSystem,
    string SourceRecordId,
    string CorrelationId,
    string IdempotencyKey,
    string Disposition,
    string Status,
    string? WorkOrderSourceId,
    string PayloadHash,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset PublishedAtUtc,
    DateTimeOffset RecordedAtUtc,
    string? Readiness,
    string? ValidationIssueCode);

public sealed record WorkOrderImportRecord(
    string SourceSystem,
    string SourceId,
    string WorkOrderNumber,
    string Title,
    string WorkType,
    string Priority,
    string LifecycleStatus,
    string Readiness,
    string? ReadinessIssueCode,
    string? ReadinessIssueDetail,
    string? AssetSourceId,
    string? FunctionalLocationSourceId,
    DateTimeOffset? RequiredStartUtc,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? ScheduledStartUtc,
    decimal? EstimatedHours,
    DateTimeOffset SourceUpdatedAtUtc,
    DateTimeOffset ImportedAtUtc,
    string SourcePayloadHash);

public sealed record MajorEventImportRecord(
    string SourceSystem,
    string SourceId,
    string EventType,
    string Title,
    string Severity,
    string? ReadinessIssueCode,
    string? AssetSourceId,
    string? FunctionalLocationSourceId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    DateTimeOffset ImportedAtUtc);

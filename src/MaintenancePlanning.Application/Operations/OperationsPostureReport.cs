namespace MaintenancePlanning.Application.Operations;

public sealed record OperationsPostureReport(
    bool DatabaseConfigured,
    string Status,
    string? IssueCode,
    LatestImportFreshness? LatestImport,
    StaleReceivedImportPosture StaleReceivedImports,
    IntegrationEventingPosture Eventing,
    OutboundOutboxPosture Outbox,
    DateTimeOffset CheckedAtUtc);

public sealed record LatestImportFreshness(
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
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record StaleReceivedImportPosture(
    int Count,
    int ThresholdMinutes,
    DateTimeOffset StaleBeforeUtc,
    DateTimeOffset? OldestReceivedAtUtc);

public sealed record IntegrationEventingPosture(
    string PublishMode,
    int QueueDepth,
    int DeadLetterCount,
    string? LastFailureCode);

public sealed record OutboundOutboxPosture(
    bool Configured,
    int PendingCount,
    int FailedCount,
    string? LastFailureCode);

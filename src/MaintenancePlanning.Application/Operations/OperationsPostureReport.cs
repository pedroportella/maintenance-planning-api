namespace MaintenancePlanning.Application.Operations;

public sealed record OperationsPostureReport(
    bool DatabaseConfigured,
    string Status,
    string? IssueCode,
    LatestImportFreshness? LatestImport,
    IntegrationEventingPosture Eventing,
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

public sealed record IntegrationEventingPosture(
    string PublishMode,
    int QueueDepth,
    int DeadLetterCount,
    string? LastFailureCode);

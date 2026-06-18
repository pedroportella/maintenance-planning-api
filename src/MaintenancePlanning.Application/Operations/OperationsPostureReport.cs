namespace MaintenancePlanning.Application.Operations;

public sealed record OperationsPostureReport(
    bool DatabaseConfigured,
    string Status,
    LatestImportFreshness? LatestImport,
    DateTimeOffset CheckedAtUtc);

public sealed record LatestImportFreshness(
    Guid ImportId,
    string SourceSystem,
    string ImportKind,
    string Status,
    int ReceivedCount,
    int AcceptedCount,
    int RejectedCount,
    int IgnoredDuplicateCount,
    int IgnoredStaleCount,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? CompletedAtUtc);

namespace MaintenancePlanning.Application.Persistence;

public sealed record MigrationReadinessReport(
    bool DatabaseConfigured,
    bool DatabaseReachable,
    bool IsReady,
    string Status,
    int AppliedMigrationCount,
    int PendingMigrationCount,
    IReadOnlyList<string> PendingMigrations,
    string? LatestAppliedMigration,
    string? IssueCode,
    DateTimeOffset CheckedAtUtc);

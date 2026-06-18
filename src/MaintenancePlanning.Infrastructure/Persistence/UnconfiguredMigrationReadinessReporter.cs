using MaintenancePlanning.Application.Persistence;

namespace MaintenancePlanning.Infrastructure.Persistence;

internal sealed class UnconfiguredMigrationReadinessReporter : IMigrationReadinessReporter
{
    public Task<MigrationReadinessReport> CheckAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new MigrationReadinessReport(
            DatabaseConfigured: false,
            DatabaseReachable: false,
            IsReady: true,
            Status: "database-not-configured",
            AppliedMigrationCount: 0,
            PendingMigrationCount: 0,
            PendingMigrations: Array.Empty<string>(),
            LatestAppliedMigration: null,
            IssueCode: null,
            CheckedAtUtc: DateTimeOffset.UtcNow));
    }
}

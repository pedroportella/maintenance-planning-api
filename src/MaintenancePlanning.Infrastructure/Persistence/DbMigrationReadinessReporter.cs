using MaintenancePlanning.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MaintenancePlanning.Infrastructure.Persistence;

internal sealed class DbMigrationReadinessReporter(
    MaintenancePlanningDbContext dbContext,
    ILogger<DbMigrationReadinessReporter> logger) : IMigrationReadinessReporter
{
    public async Task<MigrationReadinessReport> CheckAsync(CancellationToken cancellationToken)
    {
        var checkedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return NotReady(
                    databaseReachable: false,
                    status: "database-unreachable",
                    issueCode: "database-unreachable",
                    checkedAtUtc);
            }

            var applied = await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken);
            var pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            var appliedMigrations = applied.ToArray();
            var pendingMigrations = pending.ToArray();

            if (pendingMigrations.Length > 0)
            {
                return new MigrationReadinessReport(
                    DatabaseConfigured: true,
                    DatabaseReachable: true,
                    IsReady: false,
                    Status: "pending-migrations",
                    AppliedMigrationCount: appliedMigrations.Length,
                    PendingMigrationCount: pendingMigrations.Length,
                    PendingMigrations: pendingMigrations,
                    LatestAppliedMigration: appliedMigrations.LastOrDefault(),
                    IssueCode: "pending-migrations",
                    CheckedAtUtc: checkedAtUtc);
            }

            return new MigrationReadinessReport(
                DatabaseConfigured: true,
                DatabaseReachable: true,
                IsReady: true,
                Status: "ready",
                AppliedMigrationCount: appliedMigrations.Length,
                PendingMigrationCount: 0,
                PendingMigrations: Array.Empty<string>(),
                LatestAppliedMigration: appliedMigrations.LastOrDefault(),
                IssueCode: null,
                CheckedAtUtc: checkedAtUtc);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Database migration readiness check failed.");
            return NotReady(
                databaseReachable: false,
                status: "database-readiness-check-failed",
                issueCode: "database-readiness-check-failed",
                checkedAtUtc);
        }
    }

    private static MigrationReadinessReport NotReady(
        bool databaseReachable,
        string status,
        string issueCode,
        DateTimeOffset checkedAtUtc) =>
        new(
            DatabaseConfigured: true,
            DatabaseReachable: databaseReachable,
            IsReady: false,
            Status: status,
            AppliedMigrationCount: 0,
            PendingMigrationCount: 0,
            PendingMigrations: Array.Empty<string>(),
            LatestAppliedMigration: null,
            IssueCode: issueCode,
            CheckedAtUtc: checkedAtUtc);
}

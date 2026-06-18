namespace MaintenancePlanning.Application.Persistence;

public interface IMigrationReadinessReporter
{
    Task<MigrationReadinessReport> CheckAsync(CancellationToken cancellationToken);
}

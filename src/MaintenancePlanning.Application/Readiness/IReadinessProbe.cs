namespace MaintenancePlanning.Application.Readiness;

public interface IReadinessProbe
{
    Task<ReadinessSnapshot> CheckAsync(CancellationToken cancellationToken);
}

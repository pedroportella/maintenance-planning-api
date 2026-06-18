namespace MaintenancePlanning.Api.Middleware;

public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; set; }
}

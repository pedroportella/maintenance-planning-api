namespace MaintenancePlanning.Api.Health;

public sealed record HealthEndpointResponse(
    string Status,
    string CorrelationId,
    IReadOnlyList<HealthEndpointCheckResponse> Checks);

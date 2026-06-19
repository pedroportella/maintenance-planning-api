namespace MaintenancePlanning.Application.Planning;

public interface IPlanningService
{
    Task<PlanningProcessingOutcome<PlanningRunResult>> CreatePlanningRunAsync(
        CreatePlanningRunRequest request,
        CancellationToken cancellationToken);

    Task<PlanningProcessingOutcome<PlanningRunResult>> GetPlanningRunAsync(
        Guid planningRunId,
        CancellationToken cancellationToken);

    Task<PlanningProcessingOutcome<PlanningRecommendationsResult>> GetRecommendationsAsync(
        Guid planningRunId,
        CancellationToken cancellationToken);

    Task<PlanningProcessingOutcome<WorkOrderQueryResult>> QueryWorkOrdersAsync(
        WorkOrderQueryRequest request,
        CancellationToken cancellationToken);

    Task<PlanningProcessingOutcome<WorkOrderDetailResult>> GetWorkOrderAsync(
        Guid workOrderId,
        CancellationToken cancellationToken);

    Task<PlanningProcessingOutcome<PackageDecisionResult>> RecordPackageDecisionAsync(
        Guid packageId,
        RecordPackageDecisionRequest request,
        CancellationToken cancellationToken);
}

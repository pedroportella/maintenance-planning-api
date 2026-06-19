using MaintenancePlanning.Application.Planning;
using MaintenancePlanning.Domain.Planning;

namespace MaintenancePlanning.Infrastructure.Persistence;

internal sealed class UnavailablePlanningStore : IPlanningStore
{
    public bool IsConfigured => false;

    public Task<PlanningCandidateSnapshot> LoadCandidateSnapshotAsync(
        DateTimeOffset horizonStartUtc,
        DateTimeOffset horizonEndUtc,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Planning persistence is not configured.");

    public Task SavePlanningRunAsync(
        PlanningRun planningRun,
        IReadOnlyList<WorkOrderPackage> packages,
        IReadOnlyList<PackageItem> packageItems,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Planning persistence is not configured.");

    public Task<StoredPlanningRun?> FindPlanningRunAsync(
        Guid planningRunId,
        CancellationToken cancellationToken) =>
        Task.FromResult<StoredPlanningRun?>(null);

    public Task<StoredPlanningRun?> FindPlanningRunWithRecommendationsAsync(
        Guid planningRunId,
        CancellationToken cancellationToken) =>
        Task.FromResult<StoredPlanningRun?>(null);

    public Task<StoredPlanningPackage?> FindPackageAsync(
        Guid packageId,
        CancellationToken cancellationToken) =>
        Task.FromResult<StoredPlanningPackage?>(null);

    public Task<WorkOrderQueryPage> QueryWorkOrdersAsync(
        WorkOrderQuerySpec query,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Planning persistence is not configured.");

    public Task<PlanningWorkOrderSnapshot?> FindWorkOrderAsync(
        Guid workOrderId,
        CancellationToken cancellationToken) =>
        Task.FromResult<PlanningWorkOrderSnapshot?>(null);

    public Task<IReadOnlyList<StoredPlannerDecision>> SavePackageDecisionAsync(
        Guid packageId,
        PlannerDecisionType decision,
        string reasonCode,
        string? notes,
        string decidedBy,
        DateTimeOffset decidedAtUtc,
        IReadOnlyList<Guid> workOrderIds,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Planning persistence is not configured.");
}

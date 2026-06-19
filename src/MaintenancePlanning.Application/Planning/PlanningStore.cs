using MaintenancePlanning.Domain.Planning;

namespace MaintenancePlanning.Application.Planning;

public interface IPlanningStore
{
    bool IsConfigured { get; }

    Task<PlanningCandidateSnapshot> LoadCandidateSnapshotAsync(
        DateTimeOffset horizonStartUtc,
        DateTimeOffset horizonEndUtc,
        CancellationToken cancellationToken);

    Task SavePlanningRunAsync(
        PlanningRun planningRun,
        IReadOnlyList<WorkOrderPackage> packages,
        IReadOnlyList<PackageItem> packageItems,
        CancellationToken cancellationToken);

    Task<StoredPlanningRun?> FindPlanningRunAsync(
        Guid planningRunId,
        CancellationToken cancellationToken);

    Task<StoredPlanningRun?> FindPlanningRunWithRecommendationsAsync(
        Guid planningRunId,
        CancellationToken cancellationToken);

    Task<StoredPlanningPackage?> FindPackageAsync(
        Guid packageId,
        CancellationToken cancellationToken);

    Task<WorkOrderQueryPage> QueryWorkOrdersAsync(
        WorkOrderQuerySpec query,
        CancellationToken cancellationToken);

    Task<PlanningWorkOrderSnapshot?> FindWorkOrderAsync(
        Guid workOrderId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredPlannerDecision>> SavePackageDecisionAsync(
        Guid packageId,
        PlannerDecisionType decision,
        string reasonCode,
        string? notes,
        string decidedBy,
        DateTimeOffset decidedAtUtc,
        IReadOnlyList<Guid> workOrderIds,
        CancellationToken cancellationToken);
}

public sealed record PlanningCandidateSnapshot(
    DateTimeOffset HorizonStartUtc,
    DateTimeOffset HorizonEndUtc,
    IReadOnlyList<PlanningWorkOrderSnapshot> WorkOrders,
    IReadOnlyList<PlanningMajorEventSnapshot> MajorEvents);

public sealed record PlanningWorkOrderSnapshot(
    Guid Id,
    string SourceSystem,
    string SourceId,
    string WorkOrderNumber,
    string Title,
    string WorkType,
    string Priority,
    WorkOrderLifecycleStatus Status,
    SourceDataReadiness Readiness,
    string? ReadinessIssueCode,
    string? ReadinessIssueDetail,
    DateTimeOffset? RequiredStartUtc,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? ScheduledStartUtc,
    decimal? EstimatedHours,
    DateTimeOffset SourceUpdatedAtUtc,
    DateTimeOffset ImportedAtUtc,
    Guid? AssetId,
    string? AssetNumber,
    string? AssetName,
    string? AssetCriticality,
    Guid? FunctionalLocationId,
    string? FunctionalLocationCode,
    string? FunctionalLocationName);

public sealed record WorkOrderQuerySpec(
    int Offset,
    int PageSize,
    bool Backlog,
    string? Priority,
    string? FunctionalLocation,
    SourceDataReadiness? Readiness,
    WorkOrderLifecycleStatus? Status,
    DateTimeOffset? UpdatedSinceUtc,
    DateTimeOffset? UpdatedBeforeUtc,
    string SortField,
    bool SortDescending);

public sealed record WorkOrderQueryPage(
    IReadOnlyList<PlanningWorkOrderSnapshot> Items,
    int? NextOffset);

public sealed record PlanningMajorEventSnapshot(
    Guid Id,
    Guid? AssetId,
    Guid? FunctionalLocationId,
    string EventType,
    string Title,
    string Severity,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string? ReadinessIssueCode);

public sealed record PlanningPackageDraft(
    Guid Id,
    string PackageNumber,
    string Title,
    decimal EstimatedHours,
    DateTimeOffset? PlannedStartUtc,
    DateTimeOffset? PlannedEndUtc,
    string RecommendationRationale,
    int Score,
    string Actionability,
    SourceDataReadinessSummary SourceDataReadiness,
    IReadOnlyList<RecommendationBlocker> Blockers,
    IReadOnlyList<PlanningPackageItemDraft> Items);

public sealed record PlanningPackageItemDraft(
    Guid Id,
    Guid WorkOrderId,
    int Sequence,
    string FitReason);

public sealed record RecommendationProfile(
    int Score,
    string Actionability,
    SourceDataReadinessSummary SourceDataReadiness,
    IReadOnlyList<RecommendationBlocker> Blockers,
    string Explanation);

public sealed record StoredPlanningRun(
    Guid Id,
    string RunNumber,
    PlanningRunStatus Status,
    string Horizon,
    DateTimeOffset HorizonStartUtc,
    DateTimeOffset HorizonEndUtc,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string RequestedBy,
    IReadOnlyList<StoredPlanningPackage> Packages);

public sealed record StoredPlanningPackage(
    Guid Id,
    Guid PlanningRunId,
    string PackageNumber,
    string Title,
    PackageStatus Status,
    decimal EstimatedHours,
    DateTimeOffset? PlannedStartUtc,
    DateTimeOffset? PlannedEndUtc,
    string RecommendationRationale,
    IReadOnlyList<StoredPlanningPackageItem> Items,
    IReadOnlyList<StoredPlannerDecision> Decisions);

public sealed record StoredPlanningPackageItem(
    Guid Id,
    Guid PackageId,
    int Sequence,
    string FitReason,
    PlanningWorkOrderSnapshot WorkOrder);

public sealed record StoredPlannerDecision(
    Guid Id,
    Guid PackageId,
    Guid? WorkOrderId,
    PlannerDecisionType Decision,
    string ReasonCode,
    string? Notes,
    DateTimeOffset DecidedAtUtc,
    string DecidedBy);

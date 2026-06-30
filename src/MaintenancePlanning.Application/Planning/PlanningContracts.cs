namespace MaintenancePlanning.Application.Planning;

public sealed class CreatePlanningRunRequest
{
    public string IdempotencyKey { get; init; } = "";

    public string Horizon { get; init; } = "two-week";

    public DateTimeOffset? HorizonStartUtc { get; init; }

    public DateTimeOffset? HorizonEndUtc { get; init; }

    public string? RequestedBy { get; init; }
}

public sealed class RecordPackageDecisionRequest
{
    public string Decision { get; init; } = "";

    public string ReasonCode { get; init; } = "";

    public string? Notes { get; init; }

    public string? DecidedBy { get; init; }

    public IReadOnlyList<Guid> WorkOrderIds { get; init; } = Array.Empty<Guid>();
}

public sealed class WorkOrderQueryRequest
{
    public string? Cursor { get; init; }

    public int? PageSize { get; init; }

    public bool? Backlog { get; init; }

    public string? Priority { get; init; }

    public string? FunctionalLocation { get; init; }

    public string? Readiness { get; init; }

    public string? Status { get; init; }

    public DateTimeOffset? UpdatedSinceUtc { get; init; }

    public DateTimeOffset? UpdatedBeforeUtc { get; init; }

    public string? Sort { get; init; }
}

public sealed record PlanningRunResult(
    Guid Id,
    string RunNumber,
    string Status,
    string Horizon,
    DateTimeOffset HorizonStartUtc,
    DateTimeOffset HorizonEndUtc,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string RequestedBy,
    int RecommendationCount,
    int ReadyRecommendationCount,
    int BlockedRecommendationCount);

public sealed record PlanningRecommendationsResult(
    Guid PlanningRunId,
    string RunNumber,
    string Status,
    IReadOnlyList<PackageRecommendationResult> Recommendations);

public sealed record PackageRecommendationResult(
    Guid PackageId,
    string PackageNumber,
    string Title,
    string Status,
    int Score,
    string Actionability,
    decimal EstimatedHours,
    DateTimeOffset? PlannedStartUtc,
    DateTimeOffset? PlannedEndUtc,
    string Explanation,
    SourceDataReadinessSummary SourceDataReadiness,
    IReadOnlyList<RecommendationBlocker> Blockers,
    IReadOnlyList<RecommendationWorkOrderResult> WorkOrders,
    IReadOnlyList<PlannerDecisionResult> Decisions);

public sealed record SourceDataReadinessSummary(
    string OverallStatus,
    int ReadyCount,
    int NeedsReviewCount,
    int BlockedCount,
    string Summary);

public sealed record RecommendationBlocker(
    string Code,
    string Category,
    string Severity,
    string Summary,
    IReadOnlyList<string> WorkOrderNumbers);

public sealed record RecommendationWorkOrderResult(
    Guid Id,
    string SourceSystem,
    string SourceId,
    string WorkOrderNumber,
    string Title,
    string WorkType,
    string Priority,
    string Status,
    string Readiness,
    string? ReadinessIssueCode,
    string? ReadinessIssueDetail,
    DateTimeOffset? RequiredStartUtc,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? ScheduledStartUtc,
    decimal? EstimatedHours,
    string? AssetNumber,
    string? AssetName,
    string? FunctionalLocationCode,
    string? FunctionalLocationName);

public sealed record WorkOrderQueryResult(
    IReadOnlyList<WorkOrderSummaryResult> Items,
    string? NextCursor,
    int PageSize,
    string Sort,
    WorkOrderQueryFilters AppliedFilters,
    DateTimeOffset QueriedAtUtc);

public sealed record WorkOrderQueryFilters(
    bool Backlog,
    string? Priority,
    string? FunctionalLocation,
    string? Readiness,
    string? Status,
    DateTimeOffset? UpdatedSinceUtc,
    DateTimeOffset? UpdatedBeforeUtc);

public sealed record WorkOrderSummaryResult(
    Guid Id,
    string SourceSystem,
    string SourceId,
    string WorkOrderNumber,
    string Title,
    string WorkType,
    string Priority,
    string Status,
    string Readiness,
    SourceDataIssueSummary? SourceDataIssue,
    DateTimeOffset? RequiredStartUtc,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? ScheduledStartUtc,
    decimal? EstimatedHours,
    DateTimeOffset SourceUpdatedAtUtc,
    DateTimeOffset ImportedAtUtc,
    string? AssetNumber,
    string? AssetName,
    string? AssetCriticality,
    string? FunctionalLocationCode,
    string? FunctionalLocationName);

public sealed record WorkOrderDetailResult(
    Guid Id,
    string SourceSystem,
    string SourceId,
    string WorkOrderNumber,
    string Title,
    string WorkType,
    string Priority,
    string Status,
    string Readiness,
    SourceDataIssueSummary? SourceDataIssue,
    DateTimeOffset? RequiredStartUtc,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? ScheduledStartUtc,
    decimal? EstimatedHours,
    DateTimeOffset SourceUpdatedAtUtc,
    DateTimeOffset ImportedAtUtc,
    string? AssetNumber,
    string? AssetName,
    string? AssetCriticality,
    string? FunctionalLocationCode,
    string? FunctionalLocationName);

public sealed record SourceDataIssueSummary(
    string Code,
    string Detail);

public sealed record PlannerDecisionResult(
    Guid Id,
    Guid PackageId,
    Guid? WorkOrderId,
    string Decision,
    string ReasonCode,
    string? Notes,
    DateTimeOffset DecidedAtUtc,
    string DecidedBy);

public sealed record PackageDecisionResult(
    Guid PackageId,
    string PackageNumber,
    string PackageStatus,
    IReadOnlyList<PlannerDecisionResult> Decisions);

public sealed record PlanningProblem(
    int StatusCode,
    string Title,
    string Detail,
    string Code,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record PlanningProcessingOutcome<T>(T? Result, PlanningProblem? Problem)
{
    public static PlanningProcessingOutcome<T> Success(T result) => new(result, null);

    public static PlanningProcessingOutcome<T> Failed(PlanningProblem problem) => new(default, problem);
}

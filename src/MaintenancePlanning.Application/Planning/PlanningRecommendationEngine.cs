using System.Globalization;
using MaintenancePlanning.Domain.Planning;

namespace MaintenancePlanning.Application.Planning;

public sealed class PlanningRecommendationEngine
{
    private const string ReadyNow = "ready-now";
    private const string NeedsResolution = "needs-resolution";
    private const string Blocked = "blocked";

    public IReadOnlyList<PlanningPackageDraft> BuildPackageDrafts(
        PlanningCandidateSnapshot snapshot,
        string runNumber)
    {
        return snapshot.WorkOrders
            .OrderBy(WorkOrderGroupKey, StringComparer.Ordinal)
            .ThenBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(item => PriorityRank(item.Priority))
            .ThenBy(item => item.WorkOrderNumber, StringComparer.Ordinal)
            .GroupBy(WorkOrderGroupKey, StringComparer.Ordinal)
            .Select((group, index) => BuildPackageDraft(snapshot, runNumber, index + 1, group.ToArray()))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.PackageNumber, StringComparer.Ordinal)
            .ToArray();
    }

    public RecommendationProfile BuildProfile(
        IReadOnlyList<PlanningWorkOrderSnapshot> workOrders,
        DateTimeOffset? plannedStartUtc)
    {
        var blockers = BuildBlockers(workOrders, plannedStartUtc);
        var readiness = BuildReadinessSummary(workOrders);
        var actionability = GetActionability(readiness, blockers);
        var score = Score(workOrders, plannedStartUtc, blockers, readiness);
        var explanation = BuildExplanation(workOrders, plannedStartUtc, readiness, blockers, actionability);

        return new RecommendationProfile(score, actionability, readiness, blockers, explanation);
    }

    private PlanningPackageDraft BuildPackageDraft(
        PlanningCandidateSnapshot snapshot,
        string runNumber,
        int sequence,
        IReadOnlyList<PlanningWorkOrderSnapshot> workOrders)
    {
        var matchingWindow = FindMatchingWindow(snapshot.MajorEvents, workOrders, snapshot.HorizonStartUtc, snapshot.HorizonEndUtc);
        var plannedStartUtc = matchingWindow?.StartsAtUtc ?? FindEarliestPlanningDate(workOrders, snapshot.HorizonStartUtc);
        var totalHours = workOrders.Sum(item => item.EstimatedHours ?? 0m);
        var plannedEndUtc = plannedStartUtc is null || totalHours <= 0
            ? (DateTimeOffset?)null
            : plannedStartUtc.Value.AddHours((double)Math.Max(totalHours, 1m));
        var profile = BuildProfile(workOrders, plannedStartUtc);
        var packageNumber = $"{runNumber}-PKG-{sequence.ToString("000", CultureInfo.InvariantCulture)}";

        return new PlanningPackageDraft(
            Guid.NewGuid(),
            packageNumber,
            BuildTitle(workOrders),
            totalHours,
            plannedStartUtc,
            plannedEndUtc,
            profile.Explanation,
            profile.Score,
            profile.Actionability,
            profile.SourceDataReadiness,
            profile.Blockers,
            workOrders
                .OrderBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(item => PriorityRank(item.Priority))
                .ThenBy(item => item.WorkOrderNumber, StringComparer.Ordinal)
                .Select((item, itemIndex) => new PlanningPackageItemDraft(
                    Guid.NewGuid(),
                    item.Id,
                    itemIndex + 1,
                    BuildFitReason(item, matchingWindow)))
                .ToArray());
    }

    private static RecommendationBlocker[] BuildBlockers(
        IReadOnlyList<PlanningWorkOrderSnapshot> workOrders,
        DateTimeOffset? plannedStartUtc)
    {
        var blockers = new List<RecommendationBlocker>();

        AddReadinessBlockers(blockers, workOrders);
        AddMissingEstimateBlocker(blockers, workOrders);
        AddMissingLocationBlocker(blockers, workOrders);

        if (plannedStartUtc is null)
        {
            blockers.Add(new RecommendationBlocker(
                "planning-window-needed",
                "window",
                "review",
                "A planning window is needed before this package can be acted on.",
                workOrders.Select(item => item.WorkOrderNumber).OrderBy(item => item, StringComparer.Ordinal).ToArray()));
        }

        return blockers
            .OrderByDescending(item => item.Severity == "blocked")
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddReadinessBlockers(
        List<RecommendationBlocker> blockers,
        IReadOnlyList<PlanningWorkOrderSnapshot> workOrders)
    {
        var groupedIssues = workOrders
            .Where(item => item.Readiness != SourceDataReadiness.Ready)
            .GroupBy(item => CleanIssueCode(item.ReadinessIssueCode, item.Readiness), StringComparer.Ordinal);

        foreach (var group in groupedIssues)
        {
            var issueWorkOrders = group.OrderBy(item => item.WorkOrderNumber, StringComparer.Ordinal).ToArray();
            var hasBlocked = issueWorkOrders.Any(item => item.Readiness == SourceDataReadiness.Blocked);
            blockers.Add(new RecommendationBlocker(
                group.Key,
                CategoryForIssue(group.Key),
                hasBlocked ? "blocked" : "review",
                BuildIssueSummary(group.Key, issueWorkOrders),
                issueWorkOrders.Select(item => item.WorkOrderNumber).ToArray()));
        }
    }

    private static void AddMissingEstimateBlocker(
        List<RecommendationBlocker> blockers,
        IReadOnlyList<PlanningWorkOrderSnapshot> workOrders)
    {
        var missingEstimates = workOrders
            .Where(item => item.EstimatedHours is null)
            .OrderBy(item => item.WorkOrderNumber, StringComparer.Ordinal)
            .ToArray();

        if (missingEstimates.Length == 0)
        {
            return;
        }

        blockers.Add(new RecommendationBlocker(
            "missing-estimate",
            "data",
            "review",
            "Estimated hours are needed before the package can be confidently planned.",
            missingEstimates.Select(item => item.WorkOrderNumber).ToArray()));
    }

    private static void AddMissingLocationBlocker(
        List<RecommendationBlocker> blockers,
        IReadOnlyList<PlanningWorkOrderSnapshot> workOrders)
    {
        var missingLocations = workOrders
            .Where(item => item.FunctionalLocationId is null && item.AssetId is null)
            .OrderBy(item => item.WorkOrderNumber, StringComparer.Ordinal)
            .ToArray();

        if (missingLocations.Length == 0)
        {
            return;
        }

        blockers.Add(new RecommendationBlocker(
            "missing-planning-location",
            "data",
            "review",
            "An asset or functional location is needed to group this work confidently.",
            missingLocations.Select(item => item.WorkOrderNumber).ToArray()));
    }

    private static SourceDataReadinessSummary BuildReadinessSummary(IReadOnlyList<PlanningWorkOrderSnapshot> workOrders)
    {
        var readyCount = workOrders.Count(item => item.Readiness == SourceDataReadiness.Ready);
        var needsReviewCount = workOrders.Count(item => item.Readiness == SourceDataReadiness.NeedsReview);
        var blockedCount = workOrders.Count(item => item.Readiness == SourceDataReadiness.Blocked);
        var overallStatus = blockedCount > 0
            ? nameof(SourceDataReadiness.Blocked)
            : needsReviewCount > 0
                ? nameof(SourceDataReadiness.NeedsReview)
                : nameof(SourceDataReadiness.Ready);
        var summary = $"{readyCount.ToString(CultureInfo.InvariantCulture)} ready, {needsReviewCount.ToString(CultureInfo.InvariantCulture)} need review, {blockedCount.ToString(CultureInfo.InvariantCulture)} blocked.";

        return new SourceDataReadinessSummary(overallStatus, readyCount, needsReviewCount, blockedCount, summary);
    }

    private static string GetActionability(
        SourceDataReadinessSummary readiness,
        IReadOnlyList<RecommendationBlocker> blockers)
    {
        if (readiness.BlockedCount > 0 || blockers.Any(item => item.Severity == "blocked"))
        {
            return Blocked;
        }

        return blockers.Count == 0 && readiness.NeedsReviewCount == 0
            ? ReadyNow
            : NeedsResolution;
    }

    private static int Score(
        IReadOnlyList<PlanningWorkOrderSnapshot> workOrders,
        DateTimeOffset? plannedStartUtc,
        IReadOnlyList<RecommendationBlocker> blockers,
        SourceDataReadinessSummary readiness)
    {
        if (workOrders.Count == 0)
        {
            return 0;
        }

        var readyRatio = (double)readiness.ReadyCount / workOrders.Count;
        var priorityScore = workOrders.Max(item => PriorityScore(item.Priority));
        var estimateScore = workOrders.All(item => item.EstimatedHours is > 0m) ? 10 : 0;
        var windowScore = plannedStartUtc is null ? 0 : 10;
        var dueScore = workOrders.Any(IsDueSoon) ? 10 : 0;
        var blockerPenalty = blockers.Sum(item => item.Severity == "blocked" ? 25 : 12);
        var rawScore = 30 + (int)Math.Round(readyRatio * 30, MidpointRounding.AwayFromZero)
            + priorityScore
            + estimateScore
            + windowScore
            + dueScore
            - blockerPenalty;

        return Math.Clamp(rawScore, 0, 100);
    }

    private static string BuildExplanation(
        IReadOnlyList<PlanningWorkOrderSnapshot> workOrders,
        DateTimeOffset? plannedStartUtc,
        SourceDataReadinessSummary readiness,
        IReadOnlyList<RecommendationBlocker> blockers,
        string actionability)
    {
        var workOrderText = workOrders.Count == 1
            ? "1 imported work order"
            : $"{workOrders.Count.ToString(CultureInfo.InvariantCulture)} imported work orders";
        var windowText = plannedStartUtc is null
            ? "without a confirmed planning window"
            : $"around {plannedStartUtc.Value:yyyy-MM-dd}";
        var actionText = actionability switch
        {
            ReadyNow => "ready for planner action",
            Blocked => "blocked until the listed constraints are resolved",
            _ => "needs planner review before action"
        };
        var blockerText = blockers.Count == 0
            ? "No blocker summaries were found."
            : $"{blockers.Count.ToString(CultureInfo.InvariantCulture)} blocker summary item(s) are listed.";

        return $"Groups {workOrderText} {windowText}. Source data is {readiness.OverallStatus}; this recommendation is {actionText}. {blockerText}";
    }

    private static PlanningMajorEventSnapshot? FindMatchingWindow(
        IReadOnlyList<PlanningMajorEventSnapshot> majorEvents,
        IReadOnlyList<PlanningWorkOrderSnapshot> workOrders,
        DateTimeOffset horizonStartUtc,
        DateTimeOffset horizonEndUtc)
    {
        return majorEvents
            .Where(item => item.StartsAtUtc >= horizonStartUtc && item.StartsAtUtc <= horizonEndUtc)
            .Where(item => workOrders.Any(workOrder =>
                (item.AssetId is not null && item.AssetId == workOrder.AssetId)
                || (item.FunctionalLocationId is not null && item.FunctionalLocationId == workOrder.FunctionalLocationId)))
            .OrderBy(item => item.StartsAtUtc)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static DateTimeOffset? FindEarliestPlanningDate(
        IReadOnlyList<PlanningWorkOrderSnapshot> workOrders,
        DateTimeOffset horizonStartUtc)
    {
        return workOrders
            .Select(item => item.ScheduledStartUtc ?? item.RequiredStartUtc ?? item.DueAtUtc)
            .Where(item => item is not null)
            .DefaultIfEmpty()
            .Min() is { } earliest
            ? earliest < horizonStartUtc ? horizonStartUtc : earliest
            : null;
    }

    private static string BuildTitle(IReadOnlyList<PlanningWorkOrderSnapshot> workOrders)
    {
        var first = workOrders[0];
        var location = first.FunctionalLocationCode ?? first.AssetNumber ?? "Unassigned planning data";
        return workOrders.Count == 1
            ? $"{location}: {first.Title}"
            : $"{location}: {workOrders.Count.ToString(CultureInfo.InvariantCulture)} work orders";
    }

    private static string BuildFitReason(
        PlanningWorkOrderSnapshot workOrder,
        PlanningMajorEventSnapshot? matchingWindow)
    {
        if (matchingWindow is not null)
        {
            return $"Fits the {matchingWindow.Title} planning window.";
        }

        if (workOrder.Readiness == SourceDataReadiness.Ready)
        {
            return "Work order is ready and has enough source data for planning.";
        }

        return $"Included for planner review because source data is {workOrder.Readiness}.";
    }

    private static string WorkOrderGroupKey(PlanningWorkOrderSnapshot workOrder)
    {
        if (workOrder.FunctionalLocationId is not null)
        {
            return $"fl:{workOrder.FunctionalLocationId:N}";
        }

        if (workOrder.AssetId is not null)
        {
            return $"asset:{workOrder.AssetId:N}";
        }

        return $"source:{workOrder.SourceSystem}";
    }

    private static bool IsDueSoon(PlanningWorkOrderSnapshot workOrder)
    {
        if (workOrder.DueAtUtc is null)
        {
            return false;
        }

        var start = workOrder.RequiredStartUtc ?? workOrder.SourceUpdatedAtUtc;
        return workOrder.DueAtUtc.Value <= start.AddDays(7);
    }

    private static int PriorityRank(string priority)
    {
        return priority.Trim().ToLowerInvariant() switch
        {
            "critical" => 0,
            "high" => 1,
            "medium" => 2,
            "low" => 3,
            _ => 4
        };
    }

    private static int PriorityScore(string priority)
    {
        return priority.Trim().ToLowerInvariant() switch
        {
            "critical" => 20,
            "high" => 16,
            "medium" => 10,
            "low" => 4,
            _ => 0
        };
    }

    private static string CleanIssueCode(string? issueCode, SourceDataReadiness readiness)
    {
        return string.IsNullOrWhiteSpace(issueCode)
            ? readiness == SourceDataReadiness.Blocked ? "source-data-blocked" : "source-data-review"
            : issueCode.Trim();
    }

    private static string CategoryForIssue(string issueCode)
    {
        var normalized = issueCode.ToLowerInvariant();

        if (normalized.Contains("part", StringComparison.Ordinal) || normalized.Contains("material", StringComparison.Ordinal))
        {
            return "parts";
        }

        if (normalized.Contains("crew", StringComparison.Ordinal) || normalized.Contains("capacity", StringComparison.Ordinal))
        {
            return "crew";
        }

        if (normalized.Contains("window", StringComparison.Ordinal) || normalized.Contains("access", StringComparison.Ordinal) || normalized.Contains("schedule", StringComparison.Ordinal))
        {
            return "window";
        }

        return "data";
    }

    private static string BuildIssueSummary(
        string issueCode,
        IReadOnlyList<PlanningWorkOrderSnapshot> workOrders)
    {
        var count = workOrders.Count.ToString(CultureInfo.InvariantCulture);
        return $"{count} work order(s) carry source-data issue {issueCode}.";
    }
}

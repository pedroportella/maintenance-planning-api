using MaintenancePlanning.Application.Planning;
using MaintenancePlanning.Domain.Planning;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class PlanningRecommendationEngineTests
{
    private static readonly DateTimeOffset ReferenceTime = new(2026, 01, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuildPackageDrafts_ScoresReadyWorkInsidePlanningWindow()
    {
        var functionalLocationId = Guid.NewGuid();
        var workOrder = CreateWorkOrder(
            "WO-5100",
            SourceDataReadiness.Ready,
            functionalLocationId: functionalLocationId,
            estimatedHours: 6m,
            priority: "high");
        var snapshot = new PlanningCandidateSnapshot(
            ReferenceTime,
            ReferenceTime.AddDays(14),
            new[] { workOrder },
            new[]
            {
                new PlanningMajorEventSnapshot(
                    Guid.NewGuid(),
                    null,
                    functionalLocationId,
                    "access-window",
                    "Shared access window",
                    "medium",
                    ReferenceTime.AddDays(3),
                    ReferenceTime.AddDays(4),
                    null)
            });
        var engine = new PlanningRecommendationEngine();

        var draft = Assert.Single(engine.BuildPackageDrafts(snapshot, "RUN-TEST"));

        Assert.Equal("ready-now", draft.Actionability);
        Assert.Empty(draft.Blockers);
        Assert.True(draft.Score >= 90);
        Assert.Equal(ReferenceTime.AddDays(3), draft.PlannedStartUtc);
        Assert.Contains("ready for planner action", draft.RecommendationRationale, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPackageDrafts_ExplainsBlockedSourceDataAndMissingWindow()
    {
        var workOrder = CreateWorkOrder(
            "WO-5200",
            SourceDataReadiness.Blocked,
            functionalLocationId: null,
            estimatedHours: null,
            priority: "medium",
            readinessIssueCode: "parts-unavailable",
            readinessIssueDetail: "Synthetic parts readiness issue.",
            includePlanningDates: false);
        var snapshot = new PlanningCandidateSnapshot(
            ReferenceTime,
            ReferenceTime.AddDays(14),
            new[] { workOrder },
            Array.Empty<PlanningMajorEventSnapshot>());
        var engine = new PlanningRecommendationEngine();

        var draft = Assert.Single(engine.BuildPackageDrafts(snapshot, "RUN-TEST"));

        Assert.Equal("blocked", draft.Actionability);
        Assert.Equal(nameof(SourceDataReadiness.Blocked), draft.SourceDataReadiness.OverallStatus);
        Assert.Contains(draft.Blockers, item => item.Code == "parts-unavailable" && item.Category == "parts");
        Assert.Contains(draft.Blockers, item => item.Code == "missing-estimate" && item.Category == "data");
        Assert.Contains(draft.Blockers, item => item.Code == "planning-window-needed" && item.Category == "window");
        Assert.True(draft.Score < 50);
    }

    private static PlanningWorkOrderSnapshot CreateWorkOrder(
        string workOrderNumber,
        SourceDataReadiness readiness,
        Guid? functionalLocationId,
        decimal? estimatedHours,
        string priority,
        string? readinessIssueCode = null,
        string? readinessIssueDetail = null,
        DateTimeOffset? requiredStartUtc = null,
        DateTimeOffset? dueAtUtc = null,
        bool includePlanningDates = true)
    {
        return new PlanningWorkOrderSnapshot(
            Guid.NewGuid(),
            "synthetic-source",
            workOrderNumber,
            workOrderNumber,
            $"Synthetic work {workOrderNumber}",
            "corrective",
            priority,
            readiness == SourceDataReadiness.Ready ? WorkOrderLifecycleStatus.ReadyForPlanning : WorkOrderLifecycleStatus.Imported,
            readiness,
            readinessIssueCode,
            readinessIssueDetail,
            includePlanningDates ? requiredStartUtc ?? ReferenceTime.AddDays(2) : null,
            includePlanningDates ? dueAtUtc ?? ReferenceTime.AddDays(6) : null,
            null,
            estimatedHours,
            ReferenceTime,
            ReferenceTime.AddMinutes(1),
            null,
            null,
            null,
            null,
            functionalLocationId,
            functionalLocationId is null ? null : "AREA-5100",
            functionalLocationId is null ? null : "Area 5100");
    }
}

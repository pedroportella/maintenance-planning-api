using MaintenancePlanning.Application.Planning;
using MaintenancePlanning.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace MaintenancePlanning.Infrastructure.Persistence;

internal sealed class EfPlanningStore(MaintenancePlanningDbContext dbContext) : IPlanningStore
{
    public bool IsConfigured => true;

    public async Task<PlanningCandidateSnapshot> LoadCandidateSnapshotAsync(
        DateTimeOffset horizonStartUtc,
        DateTimeOffset horizonEndUtc,
        CancellationToken cancellationToken)
    {
        var workOrders = await dbContext.WorkOrders
            .AsNoTracking()
            .Include(item => item.Asset)
            .Include(item => item.FunctionalLocation)
            .Where(item =>
                (item.Status == WorkOrderLifecycleStatus.Imported
                    || item.Status == WorkOrderLifecycleStatus.ReadyForPlanning
                    || item.Status == WorkOrderLifecycleStatus.Deferred)
                && (item.DueAtUtc == null || item.DueAtUtc >= horizonStartUtc)
                && (item.RequiredStartUtc == null || item.RequiredStartUtc <= horizonEndUtc))
            .OrderBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.WorkOrderNumber)
            .ToArrayAsync(cancellationToken);
        var majorEvents = await dbContext.MajorEvents
            .AsNoTracking()
            .Where(item => item.StartsAtUtc >= horizonStartUtc && item.StartsAtUtc <= horizonEndUtc)
            .OrderBy(item => item.StartsAtUtc)
            .ThenBy(item => item.Title)
            .Select(item => new PlanningMajorEventSnapshot(
                item.Id,
                item.AssetId,
                item.FunctionalLocationId,
                item.EventType,
                item.Title,
                item.Severity,
                item.StartsAtUtc,
                item.EndsAtUtc,
                item.ReadinessIssueCode))
            .ToArrayAsync(cancellationToken);

        return new PlanningCandidateSnapshot(
            horizonStartUtc,
            horizonEndUtc,
            workOrders.Select(ToWorkOrderSnapshot).ToArray(),
            majorEvents);
    }

    public async Task SavePlanningRunAsync(
        PlanningRun planningRun,
        IReadOnlyList<WorkOrderPackage> packages,
        IReadOnlyList<PackageItem> packageItems,
        CancellationToken cancellationToken)
    {
        await dbContext.PlanningRuns.AddAsync(planningRun, cancellationToken);

        foreach (var package in packages)
        {
            await dbContext.WorkOrderPackages.AddAsync(package, cancellationToken);
        }

        foreach (var packageItem in packageItems)
        {
            await dbContext.PackageItems.AddAsync(packageItem, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<StoredPlanningRun?> FindPlanningRunAsync(
        Guid planningRunId,
        CancellationToken cancellationToken)
    {
        return FindRunAsync(planningRunId, cancellationToken);
    }

    public Task<StoredPlanningRun?> FindPlanningRunWithRecommendationsAsync(
        Guid planningRunId,
        CancellationToken cancellationToken)
    {
        return FindRunAsync(planningRunId, cancellationToken);
    }

    public async Task<StoredPlanningPackage?> FindPackageAsync(
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var package = await PackageQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == packageId, cancellationToken);

        return package is null ? null : ToStoredPackage(package);
    }

    public async Task<IReadOnlyList<StoredPlannerDecision>> SavePackageDecisionAsync(
        Guid packageId,
        PlannerDecisionType decision,
        string reasonCode,
        string? notes,
        string decidedBy,
        DateTimeOffset decidedAtUtc,
        IReadOnlyList<Guid> workOrderIds,
        CancellationToken cancellationToken)
    {
        var package = await dbContext.WorkOrderPackages
            .Include(item => item.Items)
            .ThenInclude(item => item.WorkOrder)
            .SingleAsync(item => item.Id == packageId, cancellationToken);
        var packageStatus = ToPackageStatus(decision);
        var selectedWorkOrderIds = workOrderIds.ToHashSet();
        var decisionEntities = new List<PlannerDecision>();

        package.Status = packageStatus;

        if (selectedWorkOrderIds.Count == 0)
        {
            decisionEntities.Add(CreateDecision(packageId, null, decision, reasonCode, notes, decidedBy, decidedAtUtc));
        }
        else
        {
            foreach (var workOrderId in selectedWorkOrderIds.OrderBy(item => item))
            {
                decisionEntities.Add(CreateDecision(packageId, workOrderId, decision, reasonCode, notes, decidedBy, decidedAtUtc));
            }
        }

        foreach (var packageItem in package.Items.Where(item => selectedWorkOrderIds.Contains(item.WorkOrderId)))
        {
            packageItem.WorkOrder.Status = decision == PlannerDecisionType.Deferred
                ? WorkOrderLifecycleStatus.Deferred
                : WorkOrderLifecycleStatus.DecisionRecorded;
        }

        await dbContext.PlannerDecisions.AddRangeAsync(decisionEntities, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return decisionEntities.Select(ToStoredDecision).ToArray();
    }

    private async Task<StoredPlanningRun?> FindRunAsync(
        Guid planningRunId,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.PlanningRuns
            .AsNoTracking()
            .Include(item => item.Packages)
            .ThenInclude(item => item.Items)
            .ThenInclude(item => item.WorkOrder)
            .ThenInclude(item => item.Asset)
            .Include(item => item.Packages)
            .ThenInclude(item => item.Items)
            .ThenInclude(item => item.WorkOrder)
            .ThenInclude(item => item.FunctionalLocation)
            .Include(item => item.Packages)
            .ThenInclude(item => item.Decisions)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == planningRunId, cancellationToken);

        return run is null
            ? null
            : new StoredPlanningRun(
                run.Id,
                run.RunNumber,
                run.Status,
                run.Horizon,
                run.HorizonStartUtc,
                run.HorizonEndUtc,
                run.StartedAtUtc,
                run.CompletedAtUtc,
                run.RequestedBy,
                run.Packages.OrderBy(item => item.PackageNumber).Select(ToStoredPackage).ToArray());
    }

    private IQueryable<WorkOrderPackage> PackageQuery()
    {
        return dbContext.WorkOrderPackages
            .Include(item => item.Items)
            .ThenInclude(item => item.WorkOrder)
            .ThenInclude(item => item.Asset)
            .Include(item => item.Items)
            .ThenInclude(item => item.WorkOrder)
            .ThenInclude(item => item.FunctionalLocation)
            .Include(item => item.Decisions)
            .AsSplitQuery();
    }

    private static StoredPlanningPackage ToStoredPackage(WorkOrderPackage package)
    {
        return new StoredPlanningPackage(
            package.Id,
            package.PlanningRunId,
            package.PackageNumber,
            package.Title,
            package.Status,
            package.EstimatedHours,
            package.PlannedStartUtc,
            package.PlannedEndUtc,
            package.RecommendationRationale,
            package.Items
                .OrderBy(item => item.Sequence)
                .Select(item => new StoredPlanningPackageItem(
                    item.Id,
                    item.WorkOrderPackageId,
                    item.Sequence,
                    item.FitReason,
                    ToWorkOrderSnapshot(item.WorkOrder)))
                .ToArray(),
            package.Decisions
                .OrderByDescending(item => item.DecidedAtUtc)
                .Select(ToStoredDecision)
                .ToArray());
    }

    private static PlanningWorkOrderSnapshot ToWorkOrderSnapshot(WorkOrder workOrder)
    {
        return new PlanningWorkOrderSnapshot(
            workOrder.Id,
            workOrder.SourceSystem,
            workOrder.SourceId,
            workOrder.WorkOrderNumber,
            workOrder.Title,
            workOrder.WorkType,
            workOrder.Priority,
            workOrder.Status,
            workOrder.Readiness,
            workOrder.ReadinessIssueCode,
            workOrder.ReadinessIssueDetail,
            workOrder.RequiredStartUtc,
            workOrder.DueAtUtc,
            workOrder.ScheduledStartUtc,
            workOrder.EstimatedHours,
            workOrder.SourceUpdatedAtUtc,
            workOrder.ImportedAtUtc,
            workOrder.AssetId,
            workOrder.Asset?.AssetNumber,
            workOrder.Asset?.Name,
            workOrder.Asset?.Criticality,
            workOrder.FunctionalLocationId,
            workOrder.FunctionalLocation?.Code,
            workOrder.FunctionalLocation?.Name);
    }

    private static PlannerDecision CreateDecision(
        Guid packageId,
        Guid? workOrderId,
        PlannerDecisionType decision,
        string reasonCode,
        string? notes,
        string decidedBy,
        DateTimeOffset decidedAtUtc)
    {
        return new PlannerDecision
        {
            Id = Guid.NewGuid(),
            WorkOrderPackageId = packageId,
            WorkOrderId = workOrderId,
            Decision = decision,
            ReasonCode = reasonCode,
            Notes = notes,
            DecidedBy = decidedBy,
            DecidedAtUtc = decidedAtUtc
        };
    }

    private static StoredPlannerDecision ToStoredDecision(PlannerDecision decision)
    {
        return new StoredPlannerDecision(
            decision.Id,
            decision.WorkOrderPackageId,
            decision.WorkOrderId,
            decision.Decision,
            decision.ReasonCode,
            decision.Notes,
            decision.DecidedAtUtc,
            decision.DecidedBy);
    }

    private static PackageStatus ToPackageStatus(PlannerDecisionType decision)
    {
        return decision switch
        {
            PlannerDecisionType.Accepted => PackageStatus.Accepted,
            PlannerDecisionType.Rejected => PackageStatus.Rejected,
            PlannerDecisionType.Deferred => PackageStatus.Deferred,
            _ => PackageStatus.Recommended
        };
    }
}

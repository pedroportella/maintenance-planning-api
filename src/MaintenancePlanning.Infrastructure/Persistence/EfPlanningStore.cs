using System.Text.Json;
using System.Text.Json.Serialization;
using MaintenancePlanning.Application.Planning;
using MaintenancePlanning.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace MaintenancePlanning.Infrastructure.Persistence;

internal sealed class EfPlanningStore(MaintenancePlanningDbContext dbContext) : IPlanningStore
{
    private const string OutboundSourceSystem = "maintenance-planning-api";
    private const string OutboundSchemaVersion = "1.0";
    private const string PlanningRunCompletedEventType = "planning.run.completed";
    private const string PackageDecisionRecordedEventType = "planning.package.decision-recorded";

    private static readonly JsonSerializerOptions OutboxJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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

        await dbContext.OutboxEvents.AddAsync(
            CreatePlanningRunCompletedEvent(planningRun, packages, packageItems),
            cancellationToken);

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

    public async Task<WorkOrderQueryPage> QueryWorkOrdersAsync(
        WorkOrderQuerySpec query,
        CancellationToken cancellationToken)
    {
        var workOrders = WorkOrderQuery();

        if (query.Backlog)
        {
            workOrders = workOrders.Where(item =>
                item.Status == WorkOrderLifecycleStatus.Imported
                || item.Status == WorkOrderLifecycleStatus.ReadyForPlanning
                || item.Status == WorkOrderLifecycleStatus.Deferred);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            workOrders = workOrders.Where(item => item.Priority == query.Priority);
        }

        if (!string.IsNullOrWhiteSpace(query.FunctionalLocation))
        {
            workOrders = workOrders.Where(item =>
                item.FunctionalLocation != null && item.FunctionalLocation.Code == query.FunctionalLocation);
        }

        if (query.Readiness is not null)
        {
            workOrders = workOrders.Where(item => item.Readiness == query.Readiness.Value);
        }

        if (query.Status is not null)
        {
            workOrders = workOrders.Where(item => item.Status == query.Status.Value);
        }

        if (query.UpdatedSinceUtc is not null)
        {
            workOrders = workOrders.Where(item => item.SourceUpdatedAtUtc >= query.UpdatedSinceUtc.Value);
        }

        if (query.UpdatedBeforeUtc is not null)
        {
            workOrders = workOrders.Where(item => item.SourceUpdatedAtUtc < query.UpdatedBeforeUtc.Value);
        }

        var rows = await ApplyWorkOrderSort(workOrders, query.SortField, query.SortDescending)
            .Skip(query.Offset)
            .Take(query.PageSize + 1)
            .ToArrayAsync(cancellationToken);
        var items = rows.Take(query.PageSize).Select(ToWorkOrderSnapshot).ToArray();
        var nextOffset = rows.Length > query.PageSize ? query.Offset + items.Length : (int?)null;

        return new WorkOrderQueryPage(items, nextOffset);
    }

    public async Task<PlanningWorkOrderSnapshot?> FindWorkOrderAsync(
        Guid workOrderId,
        CancellationToken cancellationToken)
    {
        var workOrder = await WorkOrderQuery()
            .SingleOrDefaultAsync(item => item.Id == workOrderId, cancellationToken);

        return workOrder is null ? null : ToWorkOrderSnapshot(workOrder);
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
        await dbContext.OutboxEvents.AddAsync(
            CreatePackageDecisionRecordedEvent(package, decision, reasonCode, decidedBy, decidedAtUtc, decisionEntities),
            cancellationToken);
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

    private IQueryable<WorkOrder> WorkOrderQuery()
    {
        return dbContext.WorkOrders
            .AsNoTracking()
            .Include(item => item.Asset)
            .Include(item => item.FunctionalLocation);
    }

    private static IOrderedQueryable<WorkOrder> ApplyWorkOrderSort(
        IQueryable<WorkOrder> query,
        string sortField,
        bool descending)
    {
        return sortField switch
        {
            "dueAtUtc" => descending
                ? query.OrderByDescending(item => item.DueAtUtc ?? DateTimeOffset.MaxValue).ThenBy(item => item.WorkOrderNumber)
                : query.OrderBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue).ThenBy(item => item.WorkOrderNumber),
            "priority" => descending
                ? query.OrderByDescending(item => item.Priority).ThenBy(item => item.WorkOrderNumber)
                : query.OrderBy(item => item.Priority).ThenBy(item => item.WorkOrderNumber),
            "requiredStartUtc" => descending
                ? query.OrderByDescending(item => item.RequiredStartUtc ?? DateTimeOffset.MaxValue).ThenBy(item => item.WorkOrderNumber)
                : query.OrderBy(item => item.RequiredStartUtc ?? DateTimeOffset.MaxValue).ThenBy(item => item.WorkOrderNumber),
            "updatedAtUtc" => descending
                ? query.OrderByDescending(item => item.SourceUpdatedAtUtc).ThenBy(item => item.WorkOrderNumber)
                : query.OrderBy(item => item.SourceUpdatedAtUtc).ThenBy(item => item.WorkOrderNumber),
            "workOrderNumber" => descending
                ? query.OrderByDescending(item => item.WorkOrderNumber)
                : query.OrderBy(item => item.WorkOrderNumber),
            _ => query.OrderBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue).ThenBy(item => item.WorkOrderNumber)
        };
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

    private static OutboxEvent CreatePlanningRunCompletedEvent(
        PlanningRun planningRun,
        IReadOnlyList<WorkOrderPackage> packages,
        IReadOnlyList<PackageItem> packageItems)
    {
        var occurredAtUtc = planningRun.CompletedAtUtc ?? planningRun.StartedAtUtc;
        var payload = new
        {
            planningRunId = planningRun.Id,
            planningRun.RunNumber,
            status = planningRun.Status.ToString(),
            planningRun.Horizon,
            planningRun.HorizonStartUtc,
            planningRun.HorizonEndUtc,
            planningRun.StartedAtUtc,
            planningRun.CompletedAtUtc,
            planningRun.RequestedBy,
            recommendationCount = packages.Count,
            workOrderCount = packageItems.Select(item => item.WorkOrderId).Distinct().Count()
        };

        return CreateOutboxEvent(
            PlanningRunCompletedEventType,
            "PlanningRun",
            planningRun.Id,
            occurredAtUtc,
            $"planning-run:{planningRun.Id:N}:completed",
            payload);
    }

    private static OutboxEvent CreatePackageDecisionRecordedEvent(
        WorkOrderPackage package,
        PlannerDecisionType decision,
        string reasonCode,
        string decidedBy,
        DateTimeOffset decidedAtUtc,
        IReadOnlyList<PlannerDecision> decisions)
    {
        var firstDecisionId = decisions.OrderBy(item => item.Id).First().Id;
        var payload = new
        {
            packageId = package.Id,
            package.PlanningRunId,
            package.PackageNumber,
            packageStatus = package.Status.ToString(),
            decision = decision.ToString(),
            reasonCode,
            decidedBy,
            decidedAtUtc,
            decisionIds = decisions.Select(item => item.Id).OrderBy(item => item).ToArray(),
            workOrderIds = decisions
                .Where(item => item.WorkOrderId is not null)
                .Select(item => item.WorkOrderId!.Value)
                .OrderBy(item => item)
                .ToArray()
        };

        return CreateOutboxEvent(
            PackageDecisionRecordedEventType,
            "WorkOrderPackage",
            package.Id,
            decidedAtUtc,
            $"package:{package.Id:N}:decision:{firstDecisionId:N}",
            payload);
    }

    private static OutboxEvent CreateOutboxEvent(
        string eventType,
        string aggregateType,
        Guid aggregateId,
        DateTimeOffset occurredAtUtc,
        string idempotencyKey,
        object payload)
    {
        var eventId = Guid.NewGuid();
        var envelope = new
        {
            eventId,
            eventType,
            schemaVersion = OutboundSchemaVersion,
            sourceSystem = OutboundSourceSystem,
            aggregateType,
            aggregateId,
            correlationId = idempotencyKey,
            occurredAt = occurredAtUtc,
            recordedAt = DateTimeOffset.UtcNow,
            idempotencyKey,
            payload
        };

        return new OutboxEvent
        {
            Id = eventId,
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            PayloadJson = JsonSerializer.Serialize(envelope, OutboxJsonOptions),
            Status = OutboxEventStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = occurredAtUtc,
            AvailableAtUtc = occurredAtUtc
        };
    }
}

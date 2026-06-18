using MaintenancePlanning.Application.Imports;
using MaintenancePlanning.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace MaintenancePlanning.Infrastructure.Persistence;

internal sealed class EfImportStore(MaintenancePlanningDbContext dbContext) : IImportStore
{
    public bool IsConfigured => true;

    public async Task<StoredImport?> FindImportAsync(
        string sourceSystem,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var import = await dbContext.IntegrationImports
            .AsNoTracking()
            .Include(item => item.Events)
            .SingleOrDefaultAsync(
                item => item.SourceSystem == sourceSystem && item.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (import is null)
        {
            return null;
        }

        return new StoredImport(
            import.Id,
            import.SourceSystem,
            import.ImportKind,
            import.IdempotencyKey,
            import.RequestHash,
            import.Status.ToString(),
            import.ReceivedCount,
            import.AcceptedCount,
            import.RejectedCount,
            import.IgnoredDuplicateCount,
            import.IgnoredStaleCount,
            import.ReceivedAtUtc,
            import.CompletedAtUtc,
            import.Events
                .OrderBy(item => item.RecordedAtUtc)
                .Select(item => new ImportEventResult(
                    item.EventId,
                    item.EventType,
                    item.SourceRecordId,
                    item.IdempotencyKey,
                    item.Disposition,
                    item.Status.ToString(),
                    item.Readiness,
                    item.ValidationIssueCode))
                .ToArray());
    }

    public async Task<StoredImport?> FindLatestImportAsync(CancellationToken cancellationToken)
    {
        var import = await dbContext.IntegrationImports
            .AsNoTracking()
            .OrderByDescending(item => item.ReceivedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return import is null
            ? null
            : new StoredImport(
                import.Id,
                import.SourceSystem,
                import.ImportKind,
                import.IdempotencyKey,
                import.RequestHash,
                import.Status.ToString(),
                import.ReceivedCount,
                import.AcceptedCount,
                import.RejectedCount,
                import.IgnoredDuplicateCount,
                import.IgnoredStaleCount,
                import.ReceivedAtUtc,
                import.CompletedAtUtc,
                Array.Empty<ImportEventResult>());
    }

    public Task<bool> HasEventIdempotencyKeyAsync(
        string sourceSystem,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return dbContext.IntegrationEvents
            .AsNoTracking()
            .AnyAsync(
                item => item.SourceSystem == sourceSystem && item.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public Task<bool> HasEventIdAsync(
        string eventId,
        CancellationToken cancellationToken)
    {
        return dbContext.IntegrationEvents
            .AsNoTracking()
            .AnyAsync(item => item.EventId == eventId, cancellationToken);
    }

    public async Task<StoredSourceRecord?> FindWorkOrderAsync(
        string sourceSystem,
        string sourceId,
        CancellationToken cancellationToken)
    {
        var workOrder = await dbContext.WorkOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.SourceSystem == sourceSystem && item.SourceId == sourceId,
                cancellationToken);

        return workOrder is null
            ? null
            : new StoredSourceRecord(workOrder.SourceSystem, workOrder.SourceId, workOrder.SourceUpdatedAtUtc);
    }

    public async Task SaveImportAsync(
        ImportPersistenceBatch batch,
        CancellationToken cancellationToken)
    {
        await dbContext.IntegrationImports.AddAsync(ToEntity(batch.Import), cancellationToken);

        foreach (var eventRecord in batch.Events)
        {
            await dbContext.IntegrationEvents.AddAsync(ToEntity(eventRecord), cancellationToken);
        }

        foreach (var workOrder in batch.WorkOrders)
        {
            await UpsertWorkOrderAsync(workOrder, cancellationToken);
        }

        foreach (var majorEvent in batch.MajorEvents)
        {
            await UpsertMajorEventAsync(majorEvent, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertWorkOrderAsync(
        WorkOrderImportRecord record,
        CancellationToken cancellationToken)
    {
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(
            item => item.SourceSystem == record.SourceSystem && item.SourceId == record.SourceId,
            cancellationToken);

        if (workOrder is null)
        {
            workOrder = new WorkOrder
            {
                Id = Guid.NewGuid(),
                SourceSystem = record.SourceSystem,
                SourceId = record.SourceId
            };
            await dbContext.WorkOrders.AddAsync(workOrder, cancellationToken);
        }

        var asset = record.AssetSourceId is null
            ? null
            : await dbContext.Assets.SingleOrDefaultAsync(
                item => item.SourceSystem == record.SourceSystem && item.SourceId == record.AssetSourceId,
                cancellationToken);
        var functionalLocation = record.FunctionalLocationSourceId is null
            ? null
            : await dbContext.FunctionalLocations.SingleOrDefaultAsync(
                item => item.SourceSystem == record.SourceSystem && item.SourceId == record.FunctionalLocationSourceId,
                cancellationToken);

        workOrder.AssetId = asset?.Id;
        workOrder.FunctionalLocationId = functionalLocation?.Id;
        workOrder.WorkOrderNumber = record.WorkOrderNumber;
        workOrder.Title = record.Title;
        workOrder.WorkType = record.WorkType;
        workOrder.Priority = record.Priority;
        workOrder.Status = Enum.Parse<WorkOrderLifecycleStatus>(record.LifecycleStatus);
        workOrder.Readiness = Enum.Parse<SourceDataReadiness>(record.Readiness);
        workOrder.ReadinessIssueCode = record.ReadinessIssueCode;
        workOrder.ReadinessIssueDetail = record.ReadinessIssueDetail;
        workOrder.RequiredStartUtc = record.RequiredStartUtc;
        workOrder.DueAtUtc = record.DueAtUtc;
        workOrder.ScheduledStartUtc = record.ScheduledStartUtc;
        workOrder.EstimatedHours = record.EstimatedHours;
        workOrder.SourceUpdatedAtUtc = record.SourceUpdatedAtUtc;
        workOrder.ImportedAtUtc = record.ImportedAtUtc;
        workOrder.SourcePayloadHash = record.SourcePayloadHash;
    }

    private async Task UpsertMajorEventAsync(
        MajorEventImportRecord record,
        CancellationToken cancellationToken)
    {
        var majorEvent = await dbContext.MajorEvents.SingleOrDefaultAsync(
            item => item.SourceSystem == record.SourceSystem && item.SourceId == record.SourceId,
            cancellationToken);

        if (majorEvent is null)
        {
            majorEvent = new MajorEvent
            {
                Id = Guid.NewGuid(),
                SourceSystem = record.SourceSystem,
                SourceId = record.SourceId
            };
            await dbContext.MajorEvents.AddAsync(majorEvent, cancellationToken);
        }

        var asset = record.AssetSourceId is null
            ? null
            : await dbContext.Assets.SingleOrDefaultAsync(
                item => item.SourceSystem == record.SourceSystem && item.SourceId == record.AssetSourceId,
                cancellationToken);
        var functionalLocation = record.FunctionalLocationSourceId is null
            ? null
            : await dbContext.FunctionalLocations.SingleOrDefaultAsync(
                item => item.SourceSystem == record.SourceSystem && item.SourceId == record.FunctionalLocationSourceId,
                cancellationToken);

        majorEvent.AssetId = asset?.Id;
        majorEvent.FunctionalLocationId = functionalLocation?.Id;
        majorEvent.EventType = record.EventType;
        majorEvent.Title = record.Title;
        majorEvent.Severity = record.Severity;
        majorEvent.StartsAtUtc = record.StartsAtUtc;
        majorEvent.EndsAtUtc = record.EndsAtUtc;
        majorEvent.ReadinessIssueCode = record.ReadinessIssueCode;
        majorEvent.ImportedAtUtc = record.ImportedAtUtc;
    }

    private static IntegrationImport ToEntity(ImportAuditRecord record)
    {
        return new IntegrationImport
        {
            Id = record.Id,
            SourceSystem = record.SourceSystem,
            ImportKind = record.ImportKind,
            IdempotencyKey = record.IdempotencyKey,
            RequestHash = record.RequestHash,
            Status = Enum.Parse<IntegrationImportStatus>(record.Status),
            ReceivedCount = record.ReceivedCount,
            AcceptedCount = record.AcceptedCount,
            RejectedCount = record.RejectedCount,
            IgnoredDuplicateCount = record.IgnoredDuplicateCount,
            IgnoredStaleCount = record.IgnoredStaleCount,
            FailureCode = null,
            ReceivedAtUtc = record.ReceivedAtUtc,
            CompletedAtUtc = record.CompletedAtUtc
        };
    }

    private static IntegrationEvent ToEntity(IntegrationEventAuditRecord record)
    {
        return new IntegrationEvent
        {
            Id = record.Id,
            IntegrationImportId = record.IntegrationImportId,
            EventId = record.EventId,
            EventType = record.EventType,
            SchemaVersion = record.SchemaVersion,
            SourceSystem = record.SourceSystem,
            SourceRecordId = record.SourceRecordId,
            CorrelationId = record.CorrelationId,
            IdempotencyKey = record.IdempotencyKey,
            Disposition = record.Disposition,
            Status = Enum.Parse<IntegrationEventStatus>(record.Status),
            WorkOrderSourceId = record.WorkOrderSourceId,
            PayloadHash = record.PayloadHash,
            OccurredAtUtc = record.OccurredAtUtc,
            PublishedAtUtc = record.PublishedAtUtc,
            RecordedAtUtc = record.RecordedAtUtc,
            Readiness = record.Readiness,
            ValidationIssueCode = record.ValidationIssueCode
        };
    }
}

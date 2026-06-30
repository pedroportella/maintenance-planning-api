using MaintenancePlanning.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace MaintenancePlanning.Infrastructure.Persistence;

internal static class MaintenancePlanningSeedData
{
    private static readonly DateTimeOffset BaseTime = new(2026, 01, 15, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid FunctionalLocationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid AssetId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ReadyWorkOrderId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid ReviewWorkOrderId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid MajorEventId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid PlanningRunId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid PackageId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid PackageItemId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid PlannerDecisionId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid IntegrationImportId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static readonly Guid IntegrationEventId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid OutboxEventId = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FunctionalLocation>().HasData(new FunctionalLocation
        {
            Id = FunctionalLocationId,
            SourceSystem = "synthetic-source",
            SourceId = "FL-1000",
            Code = "AREA-1000",
            Name = "Area 1000",
            ParentSourceId = null,
            SourceUpdatedAtUtc = BaseTime,
            ImportedAtUtc = BaseTime.AddMinutes(1)
        });

        modelBuilder.Entity<Asset>().HasData(new Asset
        {
            Id = AssetId,
            FunctionalLocationId = FunctionalLocationId,
            SourceSystem = "synthetic-source",
            SourceId = "ASSET-1000",
            AssetNumber = "AS-1000",
            Name = "Primary Pump Set",
            Criticality = "high",
            SourceUpdatedAtUtc = BaseTime,
            ImportedAtUtc = BaseTime.AddMinutes(1)
        });

        modelBuilder.Entity<WorkOrder>().HasData(
            new WorkOrder
            {
                Id = ReadyWorkOrderId,
                AssetId = AssetId,
                FunctionalLocationId = FunctionalLocationId,
                SourceSystem = "synthetic-source",
                SourceId = "WO-1000",
                WorkOrderNumber = "WO-1000",
                Title = "Replace pump seals",
                WorkType = "corrective",
                Priority = "high",
                Status = WorkOrderLifecycleStatus.ReadyForPlanning,
                Readiness = SourceDataReadiness.Ready,
                RequiredStartUtc = BaseTime.AddDays(5),
                DueAtUtc = BaseTime.AddDays(9),
                ScheduledStartUtc = null,
                EstimatedHours = 8.5m,
                SourceUpdatedAtUtc = BaseTime,
                ImportedAtUtc = BaseTime.AddMinutes(2),
                SourcePayloadHash = "sha256-ready-work-order"
            },
            new WorkOrder
            {
                Id = ReviewWorkOrderId,
                AssetId = AssetId,
                FunctionalLocationId = FunctionalLocationId,
                SourceSystem = "synthetic-source",
                SourceId = "WO-1001",
                WorkOrderNumber = "WO-1001",
                Title = "Inspect standby valve",
                WorkType = "preventive",
                Priority = "medium",
                Status = WorkOrderLifecycleStatus.Imported,
                Readiness = SourceDataReadiness.NeedsReview,
                ReadinessIssueCode = "missing-estimate",
                ReadinessIssueDetail = "Estimated effort is required before packaging.",
                RequiredStartUtc = BaseTime.AddDays(7),
                DueAtUtc = BaseTime.AddDays(14),
                ScheduledStartUtc = null,
                EstimatedHours = null,
                SourceUpdatedAtUtc = BaseTime,
                ImportedAtUtc = BaseTime.AddMinutes(3),
                SourcePayloadHash = "sha256-review-work-order"
            });

        modelBuilder.Entity<MajorEvent>().HasData(new MajorEvent
        {
            Id = MajorEventId,
            AssetId = AssetId,
            FunctionalLocationId = FunctionalLocationId,
            SourceSystem = "synthetic-source",
            SourceId = "EVT-1000",
            EventType = "access-window",
            Title = "Shared access window",
            Severity = "medium",
            StartsAtUtc = BaseTime.AddDays(5),
            EndsAtUtc = BaseTime.AddDays(6),
            ImportedAtUtc = BaseTime.AddMinutes(4)
        });

        modelBuilder.Entity<PlanningRun>().HasData(new PlanningRun
        {
            Id = PlanningRunId,
            RunNumber = "RUN-1000",
            IdempotencyKey = "seed-planning-run-2026-01-15",
            RequestHash = "sha256-seed-planning-run",
            Status = PlanningRunStatus.Completed,
            Horizon = "two-week",
            HorizonStartUtc = BaseTime.AddDays(1),
            HorizonEndUtc = BaseTime.AddDays(15),
            StartedAtUtc = BaseTime.AddMinutes(10),
            CompletedAtUtc = BaseTime.AddMinutes(11),
            RequestedBy = "local-review"
        });

        modelBuilder.Entity<WorkOrderPackage>().HasData(new WorkOrderPackage
        {
            Id = PackageId,
            PlanningRunId = PlanningRunId,
            PackageNumber = "PKG-1000",
            Title = "Pump seal work package",
            Status = PackageStatus.Recommended,
            EstimatedHours = 8.5m,
            PlannedStartUtc = BaseTime.AddDays(5),
            PlannedEndUtc = BaseTime.AddDays(5).AddHours(9),
            RecommendationRationale = "Combines ready work with the available access window."
        });

        modelBuilder.Entity<PackageItem>().HasData(new PackageItem
        {
            Id = PackageItemId,
            WorkOrderPackageId = PackageId,
            WorkOrderId = ReadyWorkOrderId,
            Sequence = 1,
            FitReason = "Work order is ready and aligns with the access window."
        });

        modelBuilder.Entity<PlannerDecision>().HasData(new PlannerDecision
        {
            Id = PlannerDecisionId,
            WorkOrderPackageId = PackageId,
            WorkOrderId = ReadyWorkOrderId,
            Decision = PlannerDecisionType.Deferred,
            ReasonCode = "awaiting-review",
            Notes = "Synthetic seed decision for local review.",
            DecidedAtUtc = BaseTime.AddMinutes(12),
            DecidedBy = "local-review"
        });

        modelBuilder.Entity<IntegrationImport>().HasData(new IntegrationImport
        {
            Id = IntegrationImportId,
            SourceSystem = "synthetic-source",
            ImportKind = "work-orders",
            IdempotencyKey = "seed-work-orders-2026-01-15",
            RequestHash = "sha256-seed-work-orders",
            Status = IntegrationImportStatus.Completed,
            ReceivedCount = 2,
            AcceptedCount = 2,
            RejectedCount = 0,
            IgnoredDuplicateCount = 0,
            IgnoredStaleCount = 0,
            FailureCode = null,
            ReceivedAtUtc = BaseTime,
            CompletedAtUtc = BaseTime.AddMinutes(4)
        });

        modelBuilder.Entity<IntegrationEvent>().HasData(new IntegrationEvent
        {
            Id = IntegrationEventId,
            IntegrationImportId = IntegrationImportId,
            EventId = "seed-event-1000",
            EventType = "work-order-imported",
            SchemaVersion = "1.0",
            SourceSystem = "synthetic-source",
            SourceRecordId = "WO-1000",
            CorrelationId = "seed-correlation-1000",
            IdempotencyKey = "seed-work-order-event-1000",
            Disposition = "accepted",
            Status = IntegrationEventStatus.Accepted,
            WorkOrderSourceId = "WO-1000",
            PayloadHash = "sha256-seed-event",
            OccurredAtUtc = BaseTime,
            PublishedAtUtc = BaseTime.AddMinutes(1),
            RecordedAtUtc = BaseTime.AddMinutes(4),
            Readiness = nameof(SourceDataReadiness.Ready),
            ValidationIssueCode = null
        });

        modelBuilder.Entity<OutboxEvent>().HasData(new OutboxEvent
        {
            Id = OutboxEventId,
            EventType = "planning.package.recommended",
            AggregateType = "WorkOrderPackage",
            AggregateId = PackageId,
            PayloadJson = "{\"eventType\":\"planning.package.recommended\",\"packageNumber\":\"PKG-1000\"}",
            Status = OutboxEventStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = BaseTime.AddMinutes(11),
            AvailableAtUtc = BaseTime.AddMinutes(11),
            PublishedAtUtc = null,
            LastErrorCode = null
        });
    }
}

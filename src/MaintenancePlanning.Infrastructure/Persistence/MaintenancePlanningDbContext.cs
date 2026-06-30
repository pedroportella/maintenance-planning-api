using MaintenancePlanning.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintenancePlanning.Infrastructure.Persistence;

public sealed class MaintenancePlanningDbContext(DbContextOptions<MaintenancePlanningDbContext> options)
    : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<FunctionalLocation> FunctionalLocations => Set<FunctionalLocation>();

    public DbSet<IntegrationEvent> IntegrationEvents => Set<IntegrationEvent>();

    public DbSet<IntegrationImport> IntegrationImports => Set<IntegrationImport>();

    public DbSet<MajorEvent> MajorEvents => Set<MajorEvent>();

    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    public DbSet<PackageItem> PackageItems => Set<PackageItem>();

    public DbSet<PlannerDecision> PlannerDecisions => Set<PlannerDecision>();

    public DbSet<PlanningRun> PlanningRuns => Set<PlanningRun>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<WorkOrderPackage> WorkOrderPackages => Set<WorkOrderPackage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("planning");

        ConfigureFunctionalLocations(modelBuilder.Entity<FunctionalLocation>());
        ConfigureAssets(modelBuilder.Entity<Asset>());
        ConfigureWorkOrders(modelBuilder.Entity<WorkOrder>());
        ConfigureMajorEvents(modelBuilder.Entity<MajorEvent>());
        ConfigurePlanningRuns(modelBuilder.Entity<PlanningRun>());
        ConfigureWorkOrderPackages(modelBuilder.Entity<WorkOrderPackage>());
        ConfigurePackageItems(modelBuilder.Entity<PackageItem>());
        ConfigurePlannerDecisions(modelBuilder.Entity<PlannerDecision>());
        ConfigureIntegrationImports(modelBuilder.Entity<IntegrationImport>());
        ConfigureIntegrationEvents(modelBuilder.Entity<IntegrationEvent>());
        ConfigureOutboxEvents(modelBuilder.Entity<OutboxEvent>());

        MaintenancePlanningSeedData.Apply(modelBuilder);
    }

    private static void ConfigureFunctionalLocations(EntityTypeBuilder<FunctionalLocation> entity)
    {
        entity.ToTable("functional_locations");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => new { item.SourceSystem, item.SourceId }).IsUnique();
        entity.HasIndex(item => item.Code).IsUnique();
        entity.Property(item => item.SourceSystem).IsRequired().HasMaxLength(80);
        entity.Property(item => item.SourceId).IsRequired().HasMaxLength(120);
        entity.Property(item => item.Code).IsRequired().HasMaxLength(120);
        entity.Property(item => item.Name).IsRequired().HasMaxLength(200);
        entity.Property(item => item.ParentSourceId).HasMaxLength(120);
    }

    private static void ConfigureAssets(EntityTypeBuilder<Asset> entity)
    {
        entity.ToTable("assets");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => new { item.SourceSystem, item.SourceId }).IsUnique();
        entity.HasIndex(item => item.AssetNumber).IsUnique();
        entity.Property(item => item.SourceSystem).IsRequired().HasMaxLength(80);
        entity.Property(item => item.SourceId).IsRequired().HasMaxLength(120);
        entity.Property(item => item.AssetNumber).IsRequired().HasMaxLength(120);
        entity.Property(item => item.Name).IsRequired().HasMaxLength(200);
        entity.Property(item => item.Criticality).IsRequired().HasMaxLength(40);
        entity
            .HasOne(item => item.FunctionalLocation)
            .WithMany(item => item.Assets)
            .HasForeignKey(item => item.FunctionalLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureWorkOrders(EntityTypeBuilder<WorkOrder> entity)
    {
        entity.ToTable("work_orders");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => new { item.SourceSystem, item.SourceId }).IsUnique();
        entity.HasIndex(item => item.WorkOrderNumber).IsUnique();
        entity.HasIndex(item => item.Readiness);
        entity.Property(item => item.SourceSystem).IsRequired().HasMaxLength(80);
        entity.Property(item => item.SourceId).IsRequired().HasMaxLength(120);
        entity.Property(item => item.WorkOrderNumber).IsRequired().HasMaxLength(120);
        entity.Property(item => item.Title).IsRequired().HasMaxLength(240);
        entity.Property(item => item.WorkType).IsRequired().HasMaxLength(80);
        entity.Property(item => item.Priority).IsRequired().HasMaxLength(40);
        entity.Property(item => item.Status).HasConversion<string>().IsRequired().HasMaxLength(40);
        entity.Property(item => item.Readiness).HasConversion<string>().IsRequired().HasMaxLength(40);
        entity.Property(item => item.ReadinessIssueCode).HasMaxLength(80);
        entity.Property(item => item.ReadinessIssueDetail).HasMaxLength(500);
        entity.Property(item => item.EstimatedHours).HasPrecision(9, 2);
        entity.Property(item => item.SourcePayloadHash).IsRequired().HasMaxLength(128);
        entity
            .HasOne(item => item.Asset)
            .WithMany(item => item.WorkOrders)
            .HasForeignKey(item => item.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
        entity
            .HasOne(item => item.FunctionalLocation)
            .WithMany(item => item.WorkOrders)
            .HasForeignKey(item => item.FunctionalLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureMajorEvents(EntityTypeBuilder<MajorEvent> entity)
    {
        entity.ToTable("major_events");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => new { item.SourceSystem, item.SourceId }).IsUnique();
        entity.Property(item => item.SourceSystem).IsRequired().HasMaxLength(80);
        entity.Property(item => item.SourceId).IsRequired().HasMaxLength(120);
        entity.Property(item => item.EventType).IsRequired().HasMaxLength(80);
        entity.Property(item => item.Title).IsRequired().HasMaxLength(240);
        entity.Property(item => item.Severity).IsRequired().HasMaxLength(40);
        entity.Property(item => item.ReadinessIssueCode).HasMaxLength(80);
        entity
            .HasOne(item => item.Asset)
            .WithMany(item => item.MajorEvents)
            .HasForeignKey(item => item.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
        entity
            .HasOne(item => item.FunctionalLocation)
            .WithMany(item => item.MajorEvents)
            .HasForeignKey(item => item.FunctionalLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePlanningRuns(EntityTypeBuilder<PlanningRun> entity)
    {
        entity.ToTable("planning_runs");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => item.RunNumber).IsUnique();
        entity.HasIndex(item => item.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
        entity.Property(item => item.RunNumber).IsRequired().HasMaxLength(80);
        entity.Property(item => item.IdempotencyKey).HasMaxLength(160);
        entity.Property(item => item.RequestHash).HasMaxLength(128);
        entity.Property(item => item.Status).HasConversion<string>().IsRequired().HasMaxLength(40);
        entity.Property(item => item.Horizon).IsRequired().HasMaxLength(80);
        entity.Property(item => item.RequestedBy).IsRequired().HasMaxLength(120);
    }

    private static void ConfigureWorkOrderPackages(EntityTypeBuilder<WorkOrderPackage> entity)
    {
        entity.ToTable("work_order_packages");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => item.PackageNumber).IsUnique();
        entity.Property(item => item.PackageNumber).IsRequired().HasMaxLength(80);
        entity.Property(item => item.Title).IsRequired().HasMaxLength(240);
        entity.Property(item => item.Status).HasConversion<string>().IsRequired().HasMaxLength(40);
        entity.Property(item => item.EstimatedHours).HasPrecision(9, 2);
        entity.Property(item => item.RecommendationRationale).IsRequired().HasMaxLength(1000);
        entity
            .HasOne(item => item.PlanningRun)
            .WithMany(item => item.Packages)
            .HasForeignKey(item => item.PlanningRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePackageItems(EntityTypeBuilder<PackageItem> entity)
    {
        entity.ToTable("package_items");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => new { item.WorkOrderPackageId, item.WorkOrderId }).IsUnique();
        entity.Property(item => item.FitReason).IsRequired().HasMaxLength(500);
        entity
            .HasOne(item => item.WorkOrderPackage)
            .WithMany(item => item.Items)
            .HasForeignKey(item => item.WorkOrderPackageId)
            .OnDelete(DeleteBehavior.Cascade);
        entity
            .HasOne(item => item.WorkOrder)
            .WithMany(item => item.PackageItems)
            .HasForeignKey(item => item.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePlannerDecisions(EntityTypeBuilder<PlannerDecision> entity)
    {
        entity.ToTable("planner_decisions");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Decision).HasConversion<string>().IsRequired().HasMaxLength(40);
        entity.Property(item => item.ReasonCode).IsRequired().HasMaxLength(80);
        entity.Property(item => item.Notes).HasMaxLength(500);
        entity.Property(item => item.DecidedBy).IsRequired().HasMaxLength(120);
        entity
            .HasOne(item => item.WorkOrderPackage)
            .WithMany(item => item.Decisions)
            .HasForeignKey(item => item.WorkOrderPackageId)
            .OnDelete(DeleteBehavior.Cascade);
        entity
            .HasOne(item => item.WorkOrder)
            .WithMany(item => item.PlannerDecisions)
            .HasForeignKey(item => item.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureIntegrationImports(EntityTypeBuilder<IntegrationImport> entity)
    {
        entity.ToTable("integration_imports");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => new { item.SourceSystem, item.IdempotencyKey }).IsUnique();
        entity.HasIndex(item => new { item.Status, item.ReceivedAtUtc });
        entity.Property(item => item.SourceSystem).IsRequired().HasMaxLength(80);
        entity.Property(item => item.ImportKind).IsRequired().HasMaxLength(80);
        entity.Property(item => item.IdempotencyKey).IsRequired().HasMaxLength(160);
        entity.Property(item => item.RequestHash).IsRequired().HasMaxLength(128);
        entity.Property(item => item.Status).HasConversion<string>().IsRequired().HasMaxLength(40);
        entity.Property(item => item.FailureCode).HasMaxLength(80);
    }

    private static void ConfigureIntegrationEvents(EntityTypeBuilder<IntegrationEvent> entity)
    {
        entity.ToTable("integration_events");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => item.EventId).IsUnique();
        entity.HasIndex(item => new { item.SourceSystem, item.IdempotencyKey }).IsUnique();
        entity.Property(item => item.EventId).IsRequired().HasMaxLength(160);
        entity.Property(item => item.EventType).IsRequired().HasMaxLength(80);
        entity.Property(item => item.SchemaVersion).IsRequired().HasMaxLength(20);
        entity.Property(item => item.SourceSystem).IsRequired().HasMaxLength(80);
        entity.Property(item => item.SourceRecordId).IsRequired().HasMaxLength(120);
        entity.Property(item => item.CorrelationId).IsRequired().HasMaxLength(160);
        entity.Property(item => item.IdempotencyKey).IsRequired().HasMaxLength(160);
        entity.Property(item => item.Disposition).IsRequired().HasMaxLength(80);
        entity.Property(item => item.Status).HasConversion<string>().IsRequired().HasMaxLength(40);
        entity.Property(item => item.WorkOrderSourceId).HasMaxLength(120);
        entity.Property(item => item.PayloadHash).IsRequired().HasMaxLength(128);
        entity.Property(item => item.Readiness).HasMaxLength(40);
        entity.Property(item => item.ValidationIssueCode).HasMaxLength(80);
        entity
            .HasOne(item => item.IntegrationImport)
            .WithMany(item => item.Events)
            .HasForeignKey(item => item.IntegrationImportId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureOutboxEvents(EntityTypeBuilder<OutboxEvent> entity)
    {
        entity.ToTable("outbox_events");
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => new { item.Status, item.AvailableAtUtc });
        entity.Property(item => item.EventType).IsRequired().HasMaxLength(160);
        entity.Property(item => item.AggregateType).IsRequired().HasMaxLength(120);
        entity.Property(item => item.PayloadJson).IsRequired().HasColumnType("nvarchar(max)");
        entity.Property(item => item.Status).HasConversion<string>().IsRequired().HasMaxLength(40);
        entity.Property(item => item.LastErrorCode).HasMaxLength(80);
    }
}

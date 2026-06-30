using MaintenancePlanning.Domain.Planning;
using MaintenancePlanning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void IntegrationImports_HasLatestFailedImportIndex()
    {
        using var dbContext = CreateDbContext();
        var entityType = dbContext.Model.FindEntityType(typeof(IntegrationImport))
            ?? throw new InvalidOperationException("IntegrationImport is not mapped.");

        Assert.Contains(
            entityType.GetIndexes(),
            index => index.Properties.Select(item => item.Name).SequenceEqual(new[] { "Status", "ReceivedAtUtc" }));
    }

    [Fact]
    public void WorkOrderPackages_DoesNotAddStatusCompoundIndexWithoutMatchingQuery()
    {
        using var dbContext = CreateDbContext();
        var entityType = dbContext.Model.FindEntityType(typeof(WorkOrderPackage))
            ?? throw new InvalidOperationException("WorkOrderPackage is not mapped.");

        Assert.DoesNotContain(
            entityType.GetIndexes(),
            index => index.Properties.Select(item => item.Name).SequenceEqual(new[] { "PlanningRunId", "Status" }));
    }

    [Fact]
    public void RequiredPersistenceTextFields_AreNonNullableInModel()
    {
        using var dbContext = CreateDbContext();

        AssertPropertiesRequired<FunctionalLocation>(
            dbContext,
            "SourceSystem",
            "SourceId",
            "Code",
            "Name");
        AssertPropertiesRequired<Asset>(
            dbContext,
            "SourceSystem",
            "SourceId",
            "AssetNumber",
            "Name",
            "Criticality");
        AssertPropertiesRequired<WorkOrder>(
            dbContext,
            "SourceSystem",
            "SourceId",
            "WorkOrderNumber",
            "Title",
            "WorkType",
            "Priority",
            "Status",
            "Readiness",
            "SourcePayloadHash");
        AssertPropertiesRequired<MajorEvent>(
            dbContext,
            "SourceSystem",
            "SourceId",
            "EventType",
            "Title",
            "Severity");
        AssertPropertiesRequired<PlanningRun>(
            dbContext,
            "RunNumber",
            "Status",
            "Horizon",
            "RequestedBy");
        AssertPropertiesRequired<WorkOrderPackage>(
            dbContext,
            "PackageNumber",
            "Title",
            "Status",
            "RecommendationRationale");
        AssertPropertiesRequired<PackageItem>(dbContext, "FitReason");
        AssertPropertiesRequired<PlannerDecision>(dbContext, "Decision", "ReasonCode", "DecidedBy");
        AssertPropertiesRequired<IntegrationImport>(
            dbContext,
            "SourceSystem",
            "ImportKind",
            "IdempotencyKey",
            "RequestHash",
            "Status");
        AssertPropertiesRequired<IntegrationEvent>(
            dbContext,
            "EventId",
            "EventType",
            "SchemaVersion",
            "SourceSystem",
            "SourceRecordId",
            "CorrelationId",
            "IdempotencyKey",
            "Disposition",
            "Status",
            "PayloadHash");
        AssertPropertiesRequired<OutboxEvent>(
            dbContext,
            "EventType",
            "AggregateType",
            "PayloadJson",
            "Status");
    }

    private static MaintenancePlanningDbContext CreateDbContext()
    {
        const string connectionString =
            "Server=localhost;Database=MaintenancePlanningModelOnly;User Id=sa;" +
            "Password=unused;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<MaintenancePlanningDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new MaintenancePlanningDbContext(options);
    }

    private static void AssertPropertiesRequired<TEntity>(
        MaintenancePlanningDbContext dbContext,
        params string[] propertyNames)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped.");

        foreach (var propertyName in propertyNames)
        {
            var property = entityType.FindProperty(propertyName)
                ?? throw new InvalidOperationException($"{typeof(TEntity).Name}.{propertyName} is not mapped.");

            Assert.False(property.IsNullable, $"{typeof(TEntity).Name}.{propertyName} should be required.");
        }
    }
}

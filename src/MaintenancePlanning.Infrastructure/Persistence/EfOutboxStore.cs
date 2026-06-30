using MaintenancePlanning.Application.Eventing;
using MaintenancePlanning.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace MaintenancePlanning.Infrastructure.Persistence;

internal sealed class EfOutboxStore(MaintenancePlanningDbContext dbContext) : IOutboxStore
{
    public bool IsConfigured => true;

    public async Task<IReadOnlyList<OutboxMessage>> LoadPendingAsync(
        int maxEvents,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken)
    {
        return await dbContext.OutboxEvents
            .AsNoTracking()
            .Where(item => item.Status == OutboxEventStatus.Pending && item.AvailableAtUtc <= dueAtUtc)
            .OrderBy(item => item.AvailableAtUtc)
            .ThenBy(item => item.CreatedAtUtc)
            .Take(maxEvents)
            .Select(item => new OutboxMessage(
                item.Id,
                item.EventType,
                item.AggregateType,
                item.AggregateId,
                item.PayloadJson,
                item.AttemptCount))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<OutboxPostureSummary> CheckPostureAsync(CancellationToken cancellationToken)
    {
        var pendingCount = await dbContext.OutboxEvents
            .AsNoTracking()
            .CountAsync(item => item.Status == OutboxEventStatus.Pending, cancellationToken);

        var failedEvents = dbContext.OutboxEvents
            .AsNoTracking()
            .Where(item => item.Status == OutboxEventStatus.Failed);

        var failedCount = await failedEvents.CountAsync(cancellationToken);
        var lastFailureCode = await failedEvents
            .OrderByDescending(item => item.AvailableAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Select(item => item.LastErrorCode)
            .FirstOrDefaultAsync(cancellationToken);

        return new OutboxPostureSummary(pendingCount, failedCount, lastFailureCode);
    }

    public async Task MarkPublishedAsync(
        Guid outboxEventId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken)
    {
        var outboxEvent = await dbContext.OutboxEvents.SingleOrDefaultAsync(
            item => item.Id == outboxEventId,
            cancellationToken);

        if (outboxEvent is null)
        {
            return;
        }

        outboxEvent.Status = OutboxEventStatus.Published;
        outboxEvent.PublishedAtUtc = publishedAtUtc;
        outboxEvent.LastErrorCode = null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid outboxEventId,
        string errorCode,
        DateTimeOffset nextAvailableAtUtc,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var outboxEvent = await dbContext.OutboxEvents.SingleOrDefaultAsync(
            item => item.Id == outboxEventId,
            cancellationToken);

        if (outboxEvent is null)
        {
            return;
        }

        outboxEvent.AttemptCount++;
        outboxEvent.LastErrorCode = CleanErrorCode(errorCode);
        outboxEvent.AvailableAtUtc = nextAvailableAtUtc;
        outboxEvent.Status = outboxEvent.AttemptCount >= maxAttempts
            ? OutboxEventStatus.Failed
            : OutboxEventStatus.Pending;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string CleanErrorCode(string errorCode)
    {
        var clean = string.IsNullOrWhiteSpace(errorCode) ? "eventbridge-publish-failed" : errorCode.Trim();
        return clean.Length <= 80 ? clean : clean[..80];
    }
}

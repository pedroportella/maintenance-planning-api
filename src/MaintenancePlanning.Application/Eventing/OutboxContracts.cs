namespace MaintenancePlanning.Application.Eventing;

public interface IOutboxDispatcher
{
    Task<OutboxDispatchResult> DispatchPendingAsync(
        int maxEvents,
        CancellationToken cancellationToken);
}

public interface IOutboxStore
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<OutboxMessage>> LoadPendingAsync(
        int maxEvents,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken);

    Task<OutboxPostureSummary> CheckPostureAsync(CancellationToken cancellationToken);

    Task MarkPublishedAsync(
        Guid outboxEventId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid outboxEventId,
        string errorCode,
        DateTimeOffset nextAvailableAtUtc,
        int maxAttempts,
        CancellationToken cancellationToken);
}

public interface IOutboundEventPublisher
{
    bool IsConfigured { get; }

    Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken);
}

public sealed record OutboxMessage(
    Guid Id,
    string EventType,
    string AggregateType,
    Guid AggregateId,
    string PayloadJson,
    int AttemptCount);

public sealed record OutboxPostureSummary(
    int PendingCount,
    int FailedCount,
    string? LastFailureCode);

public sealed record OutboxDispatchResult(
    string Status,
    int PublishedCount,
    int FailedCount,
    int SkippedCount,
    DateTimeOffset DispatchedAtUtc)
{
    public static OutboxDispatchResult Skipped(string status, int skippedCount, DateTimeOffset dispatchedAtUtc) =>
        new(status, 0, 0, skippedCount, dispatchedAtUtc);
}

public sealed class OutboundEventPublishException : Exception
{
    public OutboundEventPublishException(string errorCode)
        : base("Outbound event publish failed.")
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "eventbridge-publish-failed" : errorCode.Trim();
    }

    public string ErrorCode { get; }
}

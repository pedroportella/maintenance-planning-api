using MaintenancePlanning.Application.Eventing;
using MaintenancePlanning.Application.Operations;

namespace MaintenancePlanning.Infrastructure.Eventing;

internal sealed class UnavailableEventQueueClient : IEventQueueClient
{
    public bool IsConfigured => false;

    public Task<IReadOnlyList<QueuedEventMessage>> ReceiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QueuedEventMessage>>(Array.Empty<QueuedEventMessage>());

    public Task DeleteAsync(
        QueuedEventMessage message,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

internal sealed class UnavailableEventingPostureReporter : IEventingPostureReporter
{
    public Task<IntegrationEventingPosture> CheckAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new IntegrationEventingPosture(
            PublishMode: "not-configured",
            QueueDepth: 0,
            DeadLetterCount: 0,
            LastFailureCode: null));
}

internal sealed class UnavailableOutboxStore : IOutboxStore
{
    public bool IsConfigured => false;

    public Task<IReadOnlyList<OutboxMessage>> LoadPendingAsync(
        int maxEvents,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OutboxMessage>>(Array.Empty<OutboxMessage>());

    public Task MarkPublishedAsync(
        Guid outboxEventId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task MarkFailedAsync(
        Guid outboxEventId,
        string errorCode,
        DateTimeOffset nextAvailableAtUtc,
        int maxAttempts,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

internal sealed class UnavailableOutboundEventPublisher : IOutboundEventPublisher
{
    public bool IsConfigured => false;

    public Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken) =>
        throw new OutboundEventPublishException("eventbridge-not-configured");
}

internal sealed class UnavailableDeadLetterReplayClient : IDeadLetterReplayClient
{
    public bool IsConfigured => false;

    public Task<DeadLetterMoveTask> StartReplayAsync(
        int? maxMessagesPerSecond,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Dead-letter replay is not configured.");
}

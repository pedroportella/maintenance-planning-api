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

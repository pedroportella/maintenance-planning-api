using MaintenancePlanning.Application.Eventing;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class OutboxDispatcherTests
{
    [Fact]
    public async Task DispatchPendingAsync_PublishesAndMarksPendingEvents()
    {
        var message = new OutboxMessage(
            Guid.NewGuid(),
            "planning.run.completed",
            "PlanningRun",
            Guid.NewGuid(),
            "{\"eventType\":\"planning.run.completed\"}",
            AttemptCount: 0);
        var store = new InMemoryOutboxStore(message);
        var publisher = new RecordingPublisher();
        var dispatcher = new OutboxDispatcher(store, publisher);

        var result = await dispatcher.DispatchPendingAsync(10, CancellationToken.None);

        Assert.Equal("completed", result.Status);
        Assert.Equal(1, result.PublishedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(message.Id, publisher.Published.Single().Id);
        Assert.Equal(message.Id, store.Published.Single());
    }

    [Fact]
    public async Task DispatchPendingAsync_MarksPublishFailuresWithoutLeakingDetails()
    {
        var message = new OutboxMessage(
            Guid.NewGuid(),
            "planning.package.decision-recorded",
            "WorkOrderPackage",
            Guid.NewGuid(),
            "{\"eventType\":\"planning.package.decision-recorded\"}",
            AttemptCount: 1);
        var store = new InMemoryOutboxStore(message);
        var publisher = new RecordingPublisher(fail: true);
        var dispatcher = new OutboxDispatcher(store, publisher);

        var result = await dispatcher.DispatchPendingAsync(10, CancellationToken.None);

        Assert.Equal("completed", result.Status);
        Assert.Equal(0, result.PublishedCount);
        Assert.Equal(1, result.FailedCount);
        var failure = store.Failures.Single();
        Assert.Equal(message.Id, failure.Id);
        Assert.Equal("throttling", failure.ErrorCode);
        Assert.Equal(3, failure.MaxAttempts);
    }

    [Fact]
    public async Task DispatchPendingAsync_SkipsWhenPublisherIsNotConfigured()
    {
        var store = new InMemoryOutboxStore();
        var dispatcher = new OutboxDispatcher(store, new RecordingPublisher(configured: false));

        var result = await dispatcher.DispatchPendingAsync(10, CancellationToken.None);

        Assert.Equal("publisher-not-configured", result.Status);
        Assert.Equal(0, result.PublishedCount);
    }

    private sealed class InMemoryOutboxStore(params OutboxMessage[] messages) : IOutboxStore
    {
        private readonly IReadOnlyList<OutboxMessage> _messages = messages;

        public bool IsConfigured => true;

        public List<Guid> Published { get; } = new();

        public List<FailureRecord> Failures { get; } = new();

        public Task<IReadOnlyList<OutboxMessage>> LoadPendingAsync(
            int maxEvents,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<OutboxMessage>>(_messages.Take(maxEvents).ToArray());
        }

        public Task MarkPublishedAsync(
            Guid outboxEventId,
            DateTimeOffset publishedAtUtc,
            CancellationToken cancellationToken)
        {
            Published.Add(outboxEventId);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid outboxEventId,
            string errorCode,
            DateTimeOffset nextAvailableAtUtc,
            int maxAttempts,
            CancellationToken cancellationToken)
        {
            Failures.Add(new FailureRecord(outboxEventId, errorCode, maxAttempts));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPublisher(bool configured = true, bool fail = false) : IOutboundEventPublisher
    {
        public bool IsConfigured => configured;

        public List<OutboxMessage> Published { get; } = new();

        public Task PublishAsync(
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            if (fail)
            {
                throw new OutboundEventPublishException("throttling");
            }

            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed record FailureRecord(Guid Id, string ErrorCode, int MaxAttempts);
}

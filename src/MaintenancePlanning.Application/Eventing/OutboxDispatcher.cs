using System.Text.Json;

namespace MaintenancePlanning.Application.Eventing;

public sealed class OutboxDispatcher(
    IOutboxStore outboxStore,
    IOutboundEventPublisher publisher)
    : IOutboxDispatcher
{
    private const int DefaultMaxEvents = 10;
    private const int MaxEventsLimit = 50;
    private const int MaxAttempts = 3;
    private const int InvalidPayloadMaxAttempts = 1;
    private const string InvalidPayloadCode = "outbox-payload-invalid-json";
    private const string PublishFailedCode = "eventbridge-publish-failed";

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    public async Task<OutboxDispatchResult> DispatchPendingAsync(
        int maxEvents,
        CancellationToken cancellationToken)
    {
        var dispatchedAtUtc = DateTimeOffset.UtcNow;
        var eventLimit = Math.Clamp(maxEvents <= 0 ? DefaultMaxEvents : maxEvents, 1, MaxEventsLimit);

        if (!outboxStore.IsConfigured)
        {
            return OutboxDispatchResult.Skipped("outbox-not-configured", eventLimit, dispatchedAtUtc);
        }

        if (!publisher.IsConfigured)
        {
            return OutboxDispatchResult.Skipped("publisher-not-configured", eventLimit, dispatchedAtUtc);
        }

        var messages = await outboxStore.LoadPendingAsync(eventLimit, dispatchedAtUtc, cancellationToken);
        var published = 0;
        var failed = 0;

        foreach (var message in messages)
        {
            if (!IsValidPayloadJson(message.PayloadJson))
            {
                await MarkFailedAsync(message, InvalidPayloadCode, InvalidPayloadMaxAttempts, cancellationToken);
                failed++;
                continue;
            }

            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                await outboxStore.MarkPublishedAsync(message.Id, DateTimeOffset.UtcNow, cancellationToken);
                published++;
            }
            catch (OutboundEventPublishException exception)
            {
                await MarkFailedAsync(message, exception.ErrorCode, MaxAttempts, cancellationToken);
                failed++;
            }
            catch
            {
                await MarkFailedAsync(message, PublishFailedCode, MaxAttempts, cancellationToken);
                failed++;
            }
        }

        return new OutboxDispatchResult(
            Status: "completed",
            PublishedCount: published,
            FailedCount: failed,
            SkippedCount: 0,
            DispatchedAtUtc: DateTimeOffset.UtcNow);
    }

    private Task MarkFailedAsync(
        OutboxMessage message,
        string errorCode,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        return outboxStore.MarkFailedAsync(
            message.Id,
            CleanErrorCode(errorCode),
            DateTimeOffset.UtcNow.Add(RetryDelay),
            maxAttempts,
            cancellationToken);
    }

    private static bool IsValidPayloadJson(string payloadJson)
    {
        try
        {
            using var _ = JsonDocument.Parse(payloadJson);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string CleanErrorCode(string errorCode)
    {
        var clean = string.IsNullOrWhiteSpace(errorCode) ? PublishFailedCode : errorCode.Trim();
        return clean.Length <= 80 ? clean : clean[..80];
    }
}

using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using MaintenancePlanning.Application.Eventing;

namespace MaintenancePlanning.Infrastructure.Eventing;

internal sealed class EventBridgeOutboundEventPublisher(
    IAmazonEventBridge eventBridge,
    MaintenancePlanningEventingOptions options) : IOutboundEventPublisher
{
    private const string DefaultSource = "maintenance-planning-api";

    public bool IsConfigured => options.IsOutboundConfigured;

    public async Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new OutboundEventPublishException("eventbridge-not-configured");
        }

        var response = await eventBridge.PutEventsAsync(new PutEventsRequest
        {
            Entries = new List<PutEventsRequestEntry>
            {
                new()
                {
                    EventBusName = options.EventBusName,
                    Source = string.IsNullOrWhiteSpace(options.OutboundSource)
                        ? DefaultSource
                        : options.OutboundSource.Trim(),
                    DetailType = message.EventType,
                    Detail = message.PayloadJson,
                    Time = DateTime.UtcNow
                }
            }
        }, cancellationToken);

        if (response.FailedEntryCount > 0)
        {
            var errorCode = response.Entries.FirstOrDefault()?.ErrorCode;
            throw new OutboundEventPublishException(errorCode ?? "eventbridge-publish-failed");
        }
    }
}

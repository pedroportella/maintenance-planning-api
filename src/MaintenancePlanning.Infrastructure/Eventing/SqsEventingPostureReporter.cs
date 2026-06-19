using Amazon.SQS;
using Amazon.SQS.Model;
using MaintenancePlanning.Application.Eventing;
using MaintenancePlanning.Application.Operations;

namespace MaintenancePlanning.Infrastructure.Eventing;

internal sealed class SqsEventingPostureReporter(
    IAmazonSQS sqs,
    MaintenancePlanningEventingOptions options) : IEventingPostureReporter
{
    private static readonly List<string> QueueDepthAttributes =
    [
        QueueAttributeName.ApproximateNumberOfMessages.Value,
        QueueAttributeName.ApproximateNumberOfMessagesNotVisible.Value
    ];

    public async Task<IntegrationEventingPosture> CheckAsync(CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
        {
            return NotConfigured();
        }

        var queueDepth = await ReadDepthAsync(options.QueueUrl!, cancellationToken);
        var deadLetterCount = string.IsNullOrWhiteSpace(options.DeadLetterQueueUrl)
            ? 0
            : await ReadDepthAsync(options.DeadLetterQueueUrl, cancellationToken);

        return new IntegrationEventingPosture(
            PublishMode: "eventbridge-sqs",
            QueueDepth: queueDepth,
            DeadLetterCount: deadLetterCount,
            LastFailureCode: null);
    }

    private async Task<int> ReadDepthAsync(string queueUrl, CancellationToken cancellationToken)
    {
        var response = await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            AttributeNames = QueueDepthAttributes
        }, cancellationToken);

        return ReadAttribute(response.Attributes, QueueAttributeName.ApproximateNumberOfMessages)
            + ReadAttribute(response.Attributes, QueueAttributeName.ApproximateNumberOfMessagesNotVisible);
    }

    private static int ReadAttribute(IReadOnlyDictionary<string, string> attributes, QueueAttributeName attribute)
    {
        return attributes.TryGetValue(attribute.Value, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : 0;
    }

    private static IntegrationEventingPosture NotConfigured()
    {
        return new IntegrationEventingPosture(
            PublishMode: "not-configured",
            QueueDepth: 0,
            DeadLetterCount: 0,
            LastFailureCode: null);
    }
}

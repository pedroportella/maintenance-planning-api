using Amazon.SQS;
using Amazon.SQS.Model;
using MaintenancePlanning.Application.Eventing;

namespace MaintenancePlanning.Infrastructure.Eventing;

internal sealed class SqsEventQueueClient(
    IAmazonSQS sqs,
    MaintenancePlanningEventingOptions options) : IEventQueueClient
{
    public bool IsConfigured => options.IsConfigured;

    public async Task<IReadOnlyList<QueuedEventMessage>> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return Array.Empty<QueuedEventMessage>();
        }

        var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = options.QueueUrl,
            MaxNumberOfMessages = options.MaxMessages,
            WaitTimeSeconds = options.WaitTimeSeconds,
            VisibilityTimeout = options.VisibilityTimeoutSeconds,
            MessageSystemAttributeNames = new List<string>
            {
                MessageSystemAttributeName.ApproximateReceiveCount.Value
            }
        }, cancellationToken);

        return response.Messages
            .Select(message => new QueuedEventMessage(
                message.MessageId,
                message.ReceiptHandle,
                message.Body,
                TryParseInt(message.Attributes, "ApproximateReceiveCount")))
            .ToArray();
    }

    public Task DeleteAsync(
        QueuedEventMessage message,
        CancellationToken cancellationToken)
    {
        return sqs.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = options.QueueUrl,
            ReceiptHandle = message.ReceiptHandle
        }, cancellationToken);
    }

    private static int TryParseInt(IReadOnlyDictionary<string, string> attributes, string key)
    {
        return attributes.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : 0;
    }
}

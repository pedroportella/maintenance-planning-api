using Amazon.SQS;
using Amazon.SQS.Model;
using MaintenancePlanning.Application.Eventing;

namespace MaintenancePlanning.Infrastructure.Eventing;

internal sealed class SqsDeadLetterReplayClient(
    IAmazonSQS sqs,
    MaintenancePlanningEventingOptions options) : IDeadLetterReplayClient
{
    public bool IsConfigured => options.IsDeadLetterReplayConfigured;

    public async Task<DeadLetterMoveTask> StartReplayAsync(
        int? maxMessagesPerSecond,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Dead-letter replay is not configured.");
        }

        var sourceArn = await ResolveQueueArnAsync(
            options.DeadLetterQueueArn,
            options.DeadLetterQueueUrl,
            cancellationToken);
        var destinationArn = await ResolveQueueArnAsync(
            options.QueueArn,
            options.QueueUrl,
            cancellationToken);
        var request = new StartMessageMoveTaskRequest
        {
            SourceArn = sourceArn,
            DestinationArn = destinationArn
        };

        if (maxMessagesPerSecond is not null)
        {
            request.MaxNumberOfMessagesPerSecond = maxMessagesPerSecond.Value;
        }

        var response = await sqs.StartMessageMoveTaskAsync(request, cancellationToken);

        return new DeadLetterMoveTask(
            response.TaskHandle ?? "",
            sourceArn,
            destinationArn);
    }

    private async Task<string> ResolveQueueArnAsync(
        string? configuredArn,
        string? queueUrl,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(configuredArn))
        {
            return configuredArn.Trim();
        }

        if (string.IsNullOrWhiteSpace(queueUrl))
        {
            throw new InvalidOperationException("Queue ARN or URL is required for dead-letter replay.");
        }

        var attributes = await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            AttributeNames = new List<string> { QueueAttributeName.QueueArn.Value }
        }, cancellationToken);

        return attributes.Attributes.TryGetValue(QueueAttributeName.QueueArn.Value, out var queueArn)
            ? queueArn
            : throw new InvalidOperationException("Queue ARN attribute was not returned by SQS.");
    }
}

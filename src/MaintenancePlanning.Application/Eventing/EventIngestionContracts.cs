using MaintenancePlanning.Application.Imports;
using MaintenancePlanning.Application.Operations;

namespace MaintenancePlanning.Application.Eventing;

public interface IEventIngestionService
{
    Task<EventIngestionResult> ProcessAsync(
        QueuedEventMessage message,
        CancellationToken cancellationToken);
}

public interface IEventQueueClient
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<QueuedEventMessage>> ReceiveAsync(CancellationToken cancellationToken);

    Task DeleteAsync(
        QueuedEventMessage message,
        CancellationToken cancellationToken);
}

public interface IEventingPostureReporter
{
    Task<IntegrationEventingPosture> CheckAsync(CancellationToken cancellationToken);
}

public sealed record QueuedEventMessage(
    string MessageId,
    string ReceiptHandle,
    string Body,
    int ApproximateReceiveCount);

public sealed record EventIngestionResult(
    string MessageId,
    string Status,
    bool DeleteMessage,
    string? FailureCode,
    ImportResult? ImportResult)
{
    public static EventIngestionResult Processed(string messageId, ImportResult result) =>
        new(messageId, "processed", DeleteMessage: true, FailureCode: null, result);

    public static EventIngestionResult Failed(string messageId, string failureCode) =>
        new(messageId, "failed", DeleteMessage: true, failureCode, ImportResult: null);

    public static EventIngestionResult Retry(string messageId, string failureCode) =>
        new(messageId, "retry", DeleteMessage: false, failureCode, ImportResult: null);
}

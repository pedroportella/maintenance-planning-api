using MaintenancePlanning.Application.Eventing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MaintenancePlanning.Worker;

internal sealed class EventIngestionWorker(
    IEventQueueClient queueClient,
    IServiceScopeFactory scopeFactory,
    ILogger<EventIngestionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan UnconfiguredDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Maintenance event ingestion worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!queueClient.IsConfigured)
            {
                logger.LogWarning("Event queue is not configured; worker is waiting.");
                await DelayAsync(UnconfiguredDelay, stoppingToken);
                continue;
            }

            try
            {
                var messages = await queueClient.ReceiveAsync(stoppingToken);
                foreach (var message in messages)
                {
                    await ProcessMessageAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Event ingestion worker polling failed.");
                await DelayAsync(ErrorDelay, stoppingToken);
            }
        }

        logger.LogInformation("Maintenance event ingestion worker stopped.");
    }

    private async Task ProcessMessageAsync(
        QueuedEventMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var ingestionService = scope.ServiceProvider.GetRequiredService<IEventIngestionService>();
            var result = await ingestionService.ProcessAsync(message, cancellationToken);

            logger.LogInformation(
                "Processed queued event message {MessageId} with status {Status}, failure code {FailureCode} and receive count {ApproximateReceiveCount}.",
                message.MessageId,
                result.Status,
                result.FailureCode,
                message.ApproximateReceiveCount);

            if (result.DeleteMessage)
            {
                await queueClient.DeleteAsync(message, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Queued event message {MessageId} failed and will remain visible for retry or dead-letter handling.",
                message.MessageId);
        }
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}

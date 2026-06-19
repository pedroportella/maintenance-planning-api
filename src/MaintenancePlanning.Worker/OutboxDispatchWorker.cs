using MaintenancePlanning.Application.Eventing;
using MaintenancePlanning.Infrastructure.Eventing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MaintenancePlanning.Worker;

internal sealed class OutboxDispatchWorker(
    IOutboundEventPublisher publisher,
    MaintenancePlanningEventingOptions options,
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatchWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan UnconfiguredDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbound event outbox dispatcher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!publisher.IsConfigured)
            {
                logger.LogWarning("Outbound EventBridge publishing is not configured; dispatcher is waiting.");
                await DelayAsync(UnconfiguredDelay, stoppingToken);
                continue;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                var result = await dispatcher.DispatchPendingAsync(options.MaxMessages, stoppingToken);

                if (result.PublishedCount > 0 || result.FailedCount > 0)
                {
                    logger.LogInformation(
                        "Dispatched outbound events with {PublishedCount} published and {FailedCount} failed.",
                        result.PublishedCount,
                        result.FailedCount);
                }

                await DelayAsync(PollDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbound event outbox dispatch failed.");
                await DelayAsync(ErrorDelay, stoppingToken);
            }
        }

        logger.LogInformation("Outbound event outbox dispatcher stopped.");
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

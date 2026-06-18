using Microsoft.Extensions.Hosting;

namespace MaintenancePlanning.Worker;

internal sealed class PlanningWorker(ILogger<PlanningWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Maintenance planning worker started.");

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Maintenance planning worker stopping.");
        }
    }
}

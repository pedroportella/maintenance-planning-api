using Microsoft.Extensions.Configuration;

namespace MaintenancePlanning.Infrastructure.Eventing;

public sealed class MaintenancePlanningEventingOptions
{
    public const string SectionName = "MaintenancePlanning:Eventing";

    public bool Enabled { get; set; }

    public string? QueueUrl { get; set; }

    public string? QueueArn { get; set; }

    public string? DeadLetterQueueUrl { get; set; }

    public string? DeadLetterQueueArn { get; set; }

    public string? EventBusName { get; set; }

    public string? OutboundSource { get; set; } = "maintenance-planning-api";

    public string? Region { get; set; }

    public int MaxMessages { get; set; } = 10;

    public int WaitTimeSeconds { get; set; } = 10;

    public int VisibilityTimeoutSeconds { get; set; } = 60;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(QueueUrl);

    public bool IsOutboundConfigured => Enabled && !string.IsNullOrWhiteSpace(EventBusName);

    public bool IsDeadLetterReplayConfigured =>
        Enabled
        && (!string.IsNullOrWhiteSpace(DeadLetterQueueArn) || !string.IsNullOrWhiteSpace(DeadLetterQueueUrl))
        && (!string.IsNullOrWhiteSpace(QueueArn) || !string.IsNullOrWhiteSpace(QueueUrl));

    public static MaintenancePlanningEventingOptions Create(IConfiguration configuration)
    {
        var options = configuration
            .GetSection(SectionName)
            .Get<MaintenancePlanningEventingOptions>() ?? new MaintenancePlanningEventingOptions();

        options.QueueUrl ??= configuration["MAINTENANCE_PLANNING_WORK_QUEUE_URL"];
        options.QueueArn ??= configuration["MAINTENANCE_PLANNING_WORK_QUEUE_ARN"];
        options.DeadLetterQueueUrl ??= configuration["MAINTENANCE_PLANNING_WORK_DLQ_URL"];
        options.DeadLetterQueueArn ??= configuration["MAINTENANCE_PLANNING_WORK_DLQ_ARN"];
        options.EventBusName ??= configuration["MAINTENANCE_PLANNING_EVENT_BUS_NAME"];
        options.Region ??= configuration["AWS_REGION"] ?? configuration["AWS_DEFAULT_REGION"];
        options.Enabled = options.Enabled
            || !string.IsNullOrWhiteSpace(options.QueueUrl)
            || !string.IsNullOrWhiteSpace(options.EventBusName);
        options.MaxMessages = Math.Clamp(options.MaxMessages, 1, 10);
        options.WaitTimeSeconds = Math.Clamp(options.WaitTimeSeconds, 0, 20);
        options.VisibilityTimeoutSeconds = Math.Max(1, options.VisibilityTimeoutSeconds);

        return options;
    }
}

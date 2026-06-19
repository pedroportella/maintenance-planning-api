using Microsoft.Extensions.Configuration;

namespace MaintenancePlanning.Infrastructure.Eventing;

public sealed class MaintenancePlanningEventingOptions
{
    public const string SectionName = "MaintenancePlanning:Eventing";

    public bool Enabled { get; set; }

    public string? QueueUrl { get; set; }

    public string? DeadLetterQueueUrl { get; set; }

    public string? Region { get; set; }

    public int MaxMessages { get; set; } = 10;

    public int WaitTimeSeconds { get; set; } = 10;

    public int VisibilityTimeoutSeconds { get; set; } = 60;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(QueueUrl);

    public static MaintenancePlanningEventingOptions Create(IConfiguration configuration)
    {
        var options = configuration
            .GetSection(SectionName)
            .Get<MaintenancePlanningEventingOptions>() ?? new MaintenancePlanningEventingOptions();

        options.QueueUrl ??= configuration["MAINTENANCE_PLANNING_WORK_QUEUE_URL"];
        options.DeadLetterQueueUrl ??= configuration["MAINTENANCE_PLANNING_WORK_DLQ_URL"];
        options.Region ??= configuration["AWS_REGION"] ?? configuration["AWS_DEFAULT_REGION"];
        options.Enabled = options.Enabled || !string.IsNullOrWhiteSpace(options.QueueUrl);
        options.MaxMessages = Math.Clamp(options.MaxMessages, 1, 10);
        options.WaitTimeSeconds = Math.Clamp(options.WaitTimeSeconds, 0, 20);
        options.VisibilityTimeoutSeconds = Math.Max(1, options.VisibilityTimeoutSeconds);

        return options;
    }
}

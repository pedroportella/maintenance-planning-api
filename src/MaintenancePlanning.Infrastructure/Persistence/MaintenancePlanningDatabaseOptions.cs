namespace MaintenancePlanning.Infrastructure.Persistence;

public sealed class MaintenancePlanningDatabaseOptions
{
    public const string SectionName = "MaintenancePlanning:Database";

    public bool Enabled { get; set; }

    public string Server { get; set; } = "127.0.0.1,14333";

    public string Database { get; set; } = "MaintenancePlanning";

    public string User { get; set; } = "sa";

    public string? Password { get; set; }

    public bool Encrypt { get; set; } = true;

    public bool TrustServerCertificate { get; set; } = true;

    public int CommandTimeoutSeconds { get; set; } = 30;
}

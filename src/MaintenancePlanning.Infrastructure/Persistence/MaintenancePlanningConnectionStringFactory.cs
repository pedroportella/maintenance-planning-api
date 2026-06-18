using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MaintenancePlanning.Infrastructure.Persistence;

internal static class MaintenancePlanningConnectionStringFactory
{
    public static string Create(IConfiguration configuration, MaintenancePlanningDatabaseOptions options)
    {
        var configuredConnectionString = configuration.GetConnectionString("MaintenancePlanning");
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = options.Server,
            InitialCatalog = options.Database,
            UserID = options.User
        };

        if (!string.IsNullOrWhiteSpace(options.Password))
        {
            builder.Password = options.Password;
        }

        builder["Encrypt"] = options.Encrypt ? "True" : "False";
        builder["Trust Server Certificate"] = options.TrustServerCertificate ? "True" : "False";

        return builder.ConnectionString;
    }

    public static bool IsConfigured(IConfiguration configuration, MaintenancePlanningDatabaseOptions options)
    {
        return options.Enabled ||
            !string.IsNullOrWhiteSpace(configuration.GetConnectionString("MaintenancePlanning"));
    }
}

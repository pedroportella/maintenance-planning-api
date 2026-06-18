using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MaintenancePlanning.Infrastructure.Persistence;

public sealed class MaintenancePlanningDbContextFactory : IDesignTimeDbContextFactory<MaintenancePlanningDbContext>
{
    public MaintenancePlanningDbContext CreateDbContext(string[] args)
    {
        var options = new MaintenancePlanningDatabaseOptions
        {
            Enabled = true,
            Server = ReadEnvironment("MaintenancePlanning__Database__Server") ??
                $"127.0.0.1,{ReadEnvironment("MSSQL_HOST_PORT") ?? "14333"}",
            Database = ReadEnvironment("MaintenancePlanning__Database__Database") ?? "MaintenancePlanning",
            User = ReadEnvironment("MaintenancePlanning__Database__User") ?? "sa",
            Password = ReadEnvironment("MaintenancePlanning__Database__Password") ??
                ReadEnvironment("MSSQL_SA_PASSWORD"),
            Encrypt = ReadBoolean("MaintenancePlanning__Database__Encrypt", defaultValue: true),
            TrustServerCertificate = ReadBoolean(
                "MaintenancePlanning__Database__TrustServerCertificate",
                defaultValue: true)
        };

        var builder = new DbContextOptionsBuilder<MaintenancePlanningDbContext>();
        builder.UseSqlServer(
            MaintenancePlanningConnectionStringFactory.Create(new ConfigurationBuilder().Build(), options),
            sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(MaintenancePlanningDbContext).Assembly.GetName().Name);
                sqlOptions.CommandTimeout(options.CommandTimeoutSeconds);
                sqlOptions.EnableRetryOnFailure(3);
            });

        return new MaintenancePlanningDbContext(builder.Options);
    }

    private static string? ReadEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool ReadBoolean(string name, bool defaultValue)
    {
        var value = ReadEnvironment(name);
        return bool.TryParse(value, out var parsedValue) ? parsedValue : defaultValue;
    }
}

using MaintenancePlanning.Application.Persistence;
using MaintenancePlanning.Application.Readiness;
using MaintenancePlanning.Infrastructure.Persistence;
using MaintenancePlanning.Infrastructure.Readiness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MaintenancePlanning.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseOptions = configuration
            .GetSection(MaintenancePlanningDatabaseOptions.SectionName)
            .Get<MaintenancePlanningDatabaseOptions>() ?? new MaintenancePlanningDatabaseOptions();

        services.Configure<MaintenancePlanningDatabaseOptions>(
            configuration.GetSection(MaintenancePlanningDatabaseOptions.SectionName));

        if (MaintenancePlanningConnectionStringFactory.IsConfigured(configuration, databaseOptions))
        {
            var connectionString = MaintenancePlanningConnectionStringFactory.Create(configuration, databaseOptions);

            services.AddDbContext<MaintenancePlanningDbContext>(options =>
            {
                options.UseSqlServer(
                    connectionString,
                    sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(typeof(MaintenancePlanningDbContext).Assembly.GetName().Name);
                        sqlOptions.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
                        sqlOptions.EnableRetryOnFailure(3);
                    });
            });

            services.AddScoped<IMigrationReadinessReporter, DbMigrationReadinessReporter>();
            services.TryAddSingleton<IReadinessProbe, DatabaseReadinessProbe>();
        }
        else
        {
            services.TryAddSingleton<IMigrationReadinessReporter, UnconfiguredMigrationReadinessReporter>();
            services.TryAddSingleton<IReadinessProbe, NoExternalDependencyReadinessProbe>();
        }

        return services;
    }
}

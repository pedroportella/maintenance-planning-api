using Amazon;
using Amazon.SQS;
using MaintenancePlanning.Application.Eventing;
using MaintenancePlanning.Application.Persistence;
using MaintenancePlanning.Application.Planning;
using MaintenancePlanning.Application.Readiness;
using MaintenancePlanning.Application.Imports;
using MaintenancePlanning.Infrastructure.Eventing;
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
        var eventingOptions = MaintenancePlanningEventingOptions.Create(configuration);
        services.AddSingleton(eventingOptions);

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
            services.AddScoped<IImportStore, EfImportStore>();
            services.AddScoped<IPlanningStore, EfPlanningStore>();
            services.TryAddSingleton<IReadinessProbe, DatabaseReadinessProbe>();
        }
        else
        {
            services.TryAddSingleton<IMigrationReadinessReporter, UnconfiguredMigrationReadinessReporter>();
            services.TryAddSingleton<IImportStore, UnavailableImportStore>();
            services.TryAddSingleton<IPlanningStore, UnavailablePlanningStore>();
            services.TryAddSingleton<IReadinessProbe, NoExternalDependencyReadinessProbe>();
        }

        if (eventingOptions.IsConfigured)
        {
            services.AddSingleton<IAmazonSQS>(_ => CreateSqsClient(eventingOptions));
            services.TryAddSingleton<IEventQueueClient, SqsEventQueueClient>();
            services.TryAddSingleton<IEventingPostureReporter, SqsEventingPostureReporter>();
        }
        else
        {
            services.TryAddSingleton<IEventQueueClient, UnavailableEventQueueClient>();
            services.TryAddSingleton<IEventingPostureReporter, UnavailableEventingPostureReporter>();
        }

        return services;
    }

    private static IAmazonSQS CreateSqsClient(MaintenancePlanningEventingOptions options)
    {
        return string.IsNullOrWhiteSpace(options.Region)
            ? new AmazonSQSClient()
            : new AmazonSQSClient(RegionEndpoint.GetBySystemName(options.Region));
    }
}

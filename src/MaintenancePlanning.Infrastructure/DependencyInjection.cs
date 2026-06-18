using MaintenancePlanning.Application.Readiness;
using MaintenancePlanning.Infrastructure.Readiness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MaintenancePlanning.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IReadinessProbe, NoExternalDependencyReadinessProbe>();
        return services;
    }
}

using MaintenancePlanning.Api.Endpoints;
using MaintenancePlanning.Api.Errors;
using MaintenancePlanning.Api.Health;
using MaintenancePlanning.Api.Hosting;
using MaintenancePlanning.Api.Middleware;
using MaintenancePlanning.Infrastructure;
using Microsoft.OpenApi.Models;

namespace MaintenancePlanning.Api;

public static class ApiApplication
{
    private const int ShutdownTimeoutSeconds = 20;

    public static WebApplication Create(
        WebApplicationOptions options,
        Action<WebApplicationBuilder>? configureBuilder = null,
        Action<WebApplication>? configureApp = null)
    {
        var builder = WebApplication.CreateBuilder(options);

        ConfigureServices(builder);
        configureBuilder?.Invoke(builder);

        var app = builder.Build();

        ConfigurePipeline(app);
        configureApp?.Invoke(app);

        return app;
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Host.ConfigureHostOptions(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(ShutdownTimeoutSeconds);
        });

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        });

        builder.Services.AddProblemDetails(SafeProblemDetails.Configure);
        builder.Services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        builder.Services.AddSingleton<ApplicationLifecycleState>();

        builder.Services.AddInfrastructureServices(builder.Configuration);

        builder.Services
            .AddHealthChecks()
            .AddCheck<StartupHealthCheck>("startup", tags: new[] { HealthCheckTags.Startup })
            .AddCheck<LivenessHealthCheck>("live", tags: new[] { HealthCheckTags.Live })
            .AddCheck<ReadinessHealthCheck>("ready", tags: new[] { HealthCheckTags.Ready });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo
                {
                    Title = "Maintenance Planning API",
                    Version = "v1",
                    Description = "Production-shaped review API for synthetic maintenance-planning workflows."
                });
        });
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        var lifecycleState = app.Services.GetRequiredService<ApplicationLifecycleState>();

        app.Lifetime.ApplicationStarted.Register(lifecycleState.MarkStartupComplete);
        app.Lifetime.ApplicationStopping.Register(lifecycleState.MarkStopping);

        app.UseExceptionHandler(errorApp => errorApp.Run(SafeProblemDetails.WriteUnhandledExceptionAsync));
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<GracefulShutdownMiddleware>();

        app.UseSwagger(options =>
        {
            options.RouteTemplate = "openapi/{documentName}.json";
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "Maintenance Planning API v1");
            });
        }

        app.MapHealthEndpoints();
        app.MapOperationsEndpoints();
    }
}

using MaintenancePlanning.Api.Endpoints;
using MaintenancePlanning.Api.Errors;
using MaintenancePlanning.Api.Health;
using MaintenancePlanning.Api.Hosting;
using MaintenancePlanning.Api.Middleware;
using MaintenancePlanning.Api.Security;
using MaintenancePlanning.Application.Eventing;
using MaintenancePlanning.Application.Imports;
using MaintenancePlanning.Application.Planning;
using MaintenancePlanning.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Threading.RateLimiting;

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

        builder.Services.AddScoped<IImportService, ImportService>();
        builder.Services.AddScoped<IEventIngestionService, EventIngestionService>();
        builder.Services.AddScoped<IPlanningService, PlanningService>();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services
            .AddAuthentication(ApiAuthorization.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, TestTokenAuthenticationHandler>(
                ApiAuthorization.AuthenticationScheme,
                _ => { });
        builder.Services.AddAuthorization(options => options.AddApiPolicies());
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(
                ApiRateLimitPolicies.Command,
                httpContext =>
                {
                    var permitLimit = Math.Max(
                        1,
                        builder.Configuration.GetValue<int?>(
                            "MaintenancePlanning:Security:CommandRateLimit:PermitLimit") ?? 120);
                    var windowSeconds = Math.Max(
                        1,
                        builder.Configuration.GetValue<int?>(
                            "MaintenancePlanning:Security:CommandRateLimit:WindowSeconds") ?? 60);
                    var partitionKey =
                        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = permitLimit,
                            QueueLimit = 0,
                            Window = TimeSpan.FromSeconds(windowSeconds)
                        });
                });
        });

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
            options.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "local-test-token",
                    Description = "Use synthetic local review tokens only. Runtime JWT/OIDC issuer wiring is a later deployment concern."
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
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

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
        app.MapImportEndpoints();
        app.MapPlanningEndpoints();
        app.MapWorkOrderEndpoints();
        app.MapOperationsEndpoints();
    }
}

using MaintenancePlanning.Infrastructure;
using MaintenancePlanning.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<PlanningWorker>();

await builder.Build().RunAsync();

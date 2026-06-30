using System.Net;
using System.Net.Http.Headers;
using MaintenancePlanning.Api;
using MaintenancePlanning.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MaintenancePlanning.Api.Tests;

internal sealed class TestApiHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private TestApiHost(WebApplication app, Uri baseAddress)
    {
        _app = app;
        Client = new HttpClient { BaseAddress = baseAddress };
    }

    public HttpClient Client { get; }

    public static async Task<TestApiHost> StartAsync(
        Action<WebApplicationBuilder>? configureBuilder = null,
        Action<WebApplication>? configureApp = null,
        bool authenticated = true)
    {
        var app = ApiApplication.Create(
            new WebApplicationOptions
            {
                ApplicationName = typeof(ApiApplication).Assembly.GetName().Name,
                EnvironmentName = Environments.Production
            },
            builder =>
            {
                builder.Logging.ClearProviders();
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, 0);
                });
                configureBuilder?.Invoke(builder);
            },
            configureApp);

        await app.StartAsync();

        var baseAddress = GetBoundAddress(app);
        var host = new TestApiHost(app, baseAddress);
        if (authenticated)
        {
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenNames.Reviewer);
        }

        return host;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync(TimeSpan.FromSeconds(5));
        await _app.DisposeAsync();
    }

    private static Uri GetBoundAddress(WebApplication app)
    {
        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses;
        var address = addresses?.SingleOrDefault()
            ?? throw new InvalidOperationException("The test API host did not report a bound address.");

        return new Uri(address);
    }
}

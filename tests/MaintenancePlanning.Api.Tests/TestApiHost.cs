using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using MaintenancePlanning.Api;
using MaintenancePlanning.Api.Security;
using Microsoft.AspNetCore.Builder;
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
        var port = GetAvailablePort();
        var baseAddress = new Uri($"http://127.0.0.1:{port}");
        var app = ApiApplication.Create(
            new WebApplicationOptions
            {
                Args = new[] { "--urls", baseAddress.ToString() },
                ApplicationName = typeof(ApiApplication).Assembly.GetName().Name,
                EnvironmentName = Environments.Production
            },
            builder =>
            {
                builder.Logging.ClearProviders();
                configureBuilder?.Invoke(builder);
            },
            configureApp);

        await app.StartAsync();

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

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

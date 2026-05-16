using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Web;
using Xunit;

namespace Vulperonex.Tests.Integration.Web;

public sealed class WebHostSmokeTests
{
    [Fact]
    public async Task Given_WebHost_When_HealthEndpointIsCalled_Then_ResponseUsesWebJsonDefaults()
    {
        await using var app = await StartAppAsync();

        using var client = CreateClient(app);
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().ContainKey("status").WhoseValue.Should().Be("ok");
        body.Should().NotContainKey("Status");
    }

    [Fact]
    public async Task Given_WebHost_When_OpenApiJsonIsCalled_Then_OpenApiDocumentIsReturned()
    {
        await using var app = await StartAppAsync();

        using var client = CreateClient(app);
        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("\"openapi\"");
        json.Should().Contain("/health");
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = VulperonexWebApplication.CreateBuilder(
            new WebApplicationOptions(),
            configureDefaultLoopbackPorts: false);
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://127.0.0.1:{GetAvailablePort()}");

        var app = VulperonexWebApplication.Build(builder);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses;

        var address = addresses?.Single()
            ?? throw new InvalidOperationException("Web host did not expose a server address.");

        return new HttpClient
        {
            BaseAddress = new Uri(address),
        };
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

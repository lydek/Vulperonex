using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulperonex.Web;
using Xunit;

namespace Vulperonex.Tests.Integration.Web;

public sealed class WebHostSmokeTests
{
    [Fact]
    public void Given_DevelopmentCreateBuilder_When_DevelopmentSettingsExist_Then_TwitchClientIdIsLoaded()
    {
        var builder = VulperonexWebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Environment.EnvironmentName.Should().Be("Development");
        builder.Environment.ContentRootPath.Should().Be(AppContext.BaseDirectory);
        builder.Configuration["Twitch:ClientId"].Should().NotBeNullOrWhiteSpace();
    }

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
        json.Should().Contain("/api/twitch/auth/status");
    }

    [Fact]
    public async Task Given_WebHostWithStaticIndex_When_FrontendRouteIsCalled_Then_SpaIndexIsServed()
    {
        using var contentRoot = TestContentRoot.Create();
        await contentRoot.WriteWebRootFileAsync(
            "index.html",
            "<!doctype html><html><body><div id=\"app\">phase6-spa</div></body></html>");
        await using var app = await StartAppAsync(contentRoot.Path);

        using var client = CreateClient(app);
        var rootResponse = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var frontendRouteResponse = await client.GetAsync("/admin/rules", TestContext.Current.CancellationToken);
        var backendRouteResponse = await client.GetAsync("/api/not-found", TestContext.Current.CancellationToken);

        rootResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await rootResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should()
            .Contain("phase6-spa");

        frontendRouteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        frontendRouteResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        (await frontendRouteResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should()
            .Contain("phase6-spa");

        backendRouteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await backendRouteResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should()
            .NotContain("phase6-spa");
    }

    private static async Task<WebApplication> StartAppAsync(string? contentRootPath = null)
    {
        var builder = VulperonexWebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                ContentRootPath = contentRootPath,
            },
            configureDefaultLoopbackPorts: false);
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://127.0.0.1:{GetAvailablePort()}");
        builder.Logging.ClearProviders();

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

    private sealed class TestContentRoot : IDisposable
    {
        private TestContentRoot(string path)
        {
            Path = path;
            Directory.CreateDirectory(System.IO.Path.Combine(path, "wwwroot"));
        }

        public string Path { get; }

        public static TestContentRoot Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vulperonex-web-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TestContentRoot(path);
        }

        public Task WriteWebRootFileAsync(string relativePath, string contents)
        {
            var path = System.IO.Path.Combine(Path, "wwwroot", relativePath);
            var directory = System.IO.Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("Web root path did not have a directory.");
            Directory.CreateDirectory(directory);
            return File.WriteAllTextAsync(path, contents, TestContext.Current.CancellationToken);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

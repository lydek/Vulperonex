using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Web;
using Vulperonex.Web.Members;
using Vulperonex.Web.Workflows;
using Xunit;

namespace Vulperonex.Tests.Integration.Web;

public sealed class WebHostSmokeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

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
    public async Task Given_WebHostServices_When_WorkflowEngineIsResolved_Then_SubWorkflowExecutorDoesNotCreateDiCycle()
    {
        await using var app = BuildAppWithoutStarting();
        await using var scope = app.Services.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<WorkflowEngine>();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Given_WebHostServices_When_HostedServicesAreResolved_Then_MembersSubscribeBeforeWorkflowEngine()
    {
        await using var app = BuildAppWithoutStarting();

        var hostedServices = app.Services.GetServices<IHostedService>().ToList();

        hostedServices.FindIndex(service => service is MemberModuleHostedService)
            .Should()
            .BeLessThan(hostedServices.FindIndex(service => service is WorkflowEngineDispatcher));
    }

    [Fact]
    public async Task Given_Phase7CheckInSample_When_Imported_Then_ItMatchesSimulationChat()
    {
        var json = await File.ReadAllTextAsync(
            ResolveRepoPath("docs", "phases", "phase-7-workflow-parity", "samples", "01-checkin-cooldown.json"),
            TestContext.Current.CancellationToken);
        var request = JsonSerializer.Deserialize<WorkflowRuleUpsertRequest>(json, JsonOptions)
            ?? throw new InvalidOperationException("Sample rule JSON could not be deserialized.");
        var rule = WorkflowRuleJsonMapper.ToRule(request, "sample-checkin");

        rule.Trigger!.Filter.Should().Contain("platform", "simulation");
        rule.Actions.OfType<SendChatMessageAction>()
            .Should()
            .ContainSingle()
            .Which.TargetPlatform.Should().Be("simulation");
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

    [Fact]
    public async Task Given_WebHostWithStaticIndex_When_OverlayAliasRouteIsCalled_Then_StaticOverlayRedirectIsReturned()
    {
        using var contentRoot = TestContentRoot.Create();
        await contentRoot.WriteWebRootFileAsync(
            "index.html",
            "<!doctype html><html><body><div id=\"app\">phase7d-monitor</div></body></html>");
        await using var app = await StartAppAsync(contentRoot.Path);

        using var client = CreateClient(app, allowAutoRedirect: false);
        var overlayResponse = await client.GetAsync("/overlay/chat", TestContext.Current.CancellationToken);
        var memberResponse = await client.GetAsync("/overlay/member", TestContext.Current.CancellationToken);
        var rootResponse = await client.GetAsync("/", TestContext.Current.CancellationToken);

        overlayResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        overlayResponse.Headers.Location?.ToString().Should().Be("/overlay/chat.html");

        memberResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        memberResponse.Headers.Location?.ToString().Should().Be("/overlay/member-card.html");

        rootResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        rootResponse.Headers.TryGetValues("Content-Security-Policy", out var rootCspValues).Should().BeTrue();
        rootCspValues.Should().ContainSingle()
            .Which.Should().Contain("frame-ancestors 'none'");
    }

    private static async Task<WebApplication> StartAppAsync(string? contentRootPath = null)
    {
        var builder = CreateTestBuilder(contentRootPath);
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://127.0.0.1:{GetAvailablePort()}");

        var app = VulperonexWebApplication.Build(builder);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static WebApplication BuildAppWithoutStarting(string? contentRootPath = null)
    {
        var builder = CreateTestBuilder(contentRootPath);
        return VulperonexWebApplication.Build(builder);
    }

    private static WebApplicationBuilder CreateTestBuilder(string? contentRootPath = null)
    {
        var builder = VulperonexWebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                ContentRootPath = contentRootPath,
            },
            configureDefaultLoopbackPorts: false);

        var securityRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        builder.Configuration["Security:RootPath"] = securityRoot;
        builder.Configuration["Security:CsrfTokenPath"] = Path.Combine(securityRoot, ".admin-csrf-token");

        builder.Logging.ClearProviders();
        return builder;
    }

    private static HttpClient CreateClient(WebApplication app, bool allowAutoRedirect = true)
    {
        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses;

        var address = addresses?.Single()
            ?? throw new InvalidOperationException("Web host did not expose a server address.");

        var tokenProvider = app.Services.GetRequiredService<Vulperonex.Web.Security.AdminCsrfTokenProvider>();

        var client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
        })
        {
            BaseAddress = new Uri(address),
        };
        client.DefaultRequestHeaders.Add("X-Admin-Csrf", tokenProvider.Token);
        client.DefaultRequestHeaders.Add("Origin", address);
        client.DefaultRequestHeaders.Add("Referer", address);
        return client;
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string ResolveRepoPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{Path.Combine(segments)}'.");
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

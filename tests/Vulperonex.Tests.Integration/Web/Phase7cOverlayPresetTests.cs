using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Web;
using Xunit;

namespace Vulperonex.Tests.Integration.Web;

public sealed class Phase7cOverlayPresetTests
{
    [Fact]
    public async Task Given_BuiltInPresets_When_CatalogRequested_Then_ReturnsBuiltInsOnly()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var catalog = await client.GetAsync("/api/overlay/presets", TestContext.Current.CancellationToken);
        catalog.StatusCode.Should().Be(HttpStatusCode.OK);
        var catalogJson = await catalog.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        catalogJson.Should().Contain("\"kind\":\"builtin\"");
        catalogJson.Should().Contain("\"key\":\"vulperonex-default\"");
        catalogJson.Should().NotContain("custom:");
    }

    [Fact]
    public async Task Given_ImageAsset_When_Uploaded_Then_UrlReturnedAndFileServed()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        using var upload = BuildMultipartImage(MinimalPng, "image/png", "background.png");
        var create = await client.PostAsync("/api/overlay/assets", upload, TestContext.Current.CancellationToken);

        var createBody = await create.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        create.StatusCode.Should().Be(HttpStatusCode.OK, "response body was {0}", createBody);

        using var document = JsonDocument.Parse(createBody);
        var url = document.RootElement.GetProperty("url").GetString();
        url.Should().StartWith("/overlay/assets/");
        url.Should().EndWith(".png");

        var served = await client.GetAsync(url, TestContext.Current.CancellationToken);
        served.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Given_NonImageAsset_When_Uploaded_Then_Rejected()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        using var upload = BuildMultipartImage(Encoding.UTF8.GetBytes("not an image"), "text/plain", "evil.txt");
        var response = await client.PostAsync("/api/overlay/assets", upload, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Given_SpoofedImageAsset_When_Uploaded_Then_Rejected()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        using var upload = BuildMultipartImage(Encoding.UTF8.GetBytes("not really a png"), "image/png", "spoof.png");
        var response = await client.PostAsync("/api/overlay/assets", upload, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Given_RemovedCustomPresetEndpoint_When_Requested_Then_NotFound()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var list = await client.GetAsync("/api/overlay/custom-presets", TestContext.Current.CancellationToken);
        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 1x1 transparent PNG.
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x62, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    private static MultipartFormDataContent BuildMultipartImage(byte[] bytes, string contentType, string fileName)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var solutionRoot = ResolveSolutionRoot();
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var workingRoot = Path.Combine(Path.GetTempPath(), $"vulperonex-phase7c-{Guid.NewGuid():N}");
        var webRoot = Path.Combine(workingRoot, "wwwroot");
        CopyDirectory(Path.Combine(solutionRoot, "src", "Hosts", "Vulperonex.Web", "wwwroot"), webRoot);

        var builder = VulperonexWebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development",
                WebRootPath = webRoot,
            },
            configureDefaultLoopbackPorts: false);
        var securityRoot = Path.Combine(workingRoot, "security");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Path"] = databasePath,
            ["Security:RootPath"] = securityRoot,
            ["Security:CsrfTokenPath"] = Path.Combine(securityRoot, ".admin-csrf-token"),
        });
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://127.0.0.1:{GetAvailablePort()}");

        var app = VulperonexWebApplication.Build(builder);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient CreateClient(WebApplication app, bool allowAutoRedirect = true)
    {
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            .Single();

        var tokenProvider = app.Services.GetRequiredService<Vulperonex.Web.Security.AdminCsrfTokenProvider>();

        var client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
        })
        {
            BaseAddress = new Uri(address!)
        };

        client.DefaultRequestHeaders.Add("X-Admin-Csrf", tokenProvider.Token);
        client.DefaultRequestHeaders.Add("Origin", address);
        client.DefaultRequestHeaders.Add("Referer", address);
        return client;
    }

    private static string ResolveSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulperonex.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return directory!.FullName;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = file.Replace(source, destination, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
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

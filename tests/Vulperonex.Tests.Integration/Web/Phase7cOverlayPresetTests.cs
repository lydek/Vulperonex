using System.IO.Compression;
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
    public async Task Given_CustomHtmlPresetUpload_When_ListAndCatalogRequested_Then_MetadataIsReturnedAndStaticFileServes()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        using var upload = BuildMultipartHtml("test-html", "<!DOCTYPE html><html><body>phase7c-html</body></html>");
        var create = await client.PostAsync("/api/overlay/custom-presets", upload, TestContext.Current.CancellationToken);

        var createBody = await create.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        create.StatusCode.Should().Be(HttpStatusCode.Created, "response body was {0}", createBody);
        create.Headers.Location?.ToString().Should().Be("/overlay/custom/test-html/index.html");

        var list = await client.GetAsync("/api/overlay/custom-presets", TestContext.Current.CancellationToken);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        listJson.Should().Contain("\"slug\":\"test-html\"");
        listJson.Should().Contain("\"sizeBytes\":");
        listJson.Should().Contain("\"uploadedAt\":");
        listJson.Should().NotContain("D:\\");

        var catalog = await client.GetAsync("/api/overlay/presets", TestContext.Current.CancellationToken);
        catalog.StatusCode.Should().Be(HttpStatusCode.OK);
        var catalogJson = await catalog.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        catalogJson.Should().Contain("\"hubName\":\"chat\"");
        catalogJson.Should().Contain("\"key\":\"custom:test-html\"");
        catalogJson.Should().Contain("\"kind\":\"custom\"");

        var staticFile = await client.GetAsync("/overlay/custom/test-html/index.html", TestContext.Current.CancellationToken);
        staticFile.StatusCode.Should().Be(HttpStatusCode.OK);
        (await staticFile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("phase7c-html");
    }

    [Fact]
    public async Task Given_CustomZipPresetUpload_When_PathTraversalEntryExists_Then_RequestIsRejected()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        await using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var indexEntry = archive.CreateEntry("index.html");
            await using (var writer = new StreamWriter(indexEntry.Open(), Encoding.UTF8, leaveOpen: false))
            {
                await writer.WriteAsync("<html><body>safe</body></html>");
            }

            var traversalEntry = archive.CreateEntry("../escape.txt");
            await using var traversalWriter = new StreamWriter(traversalEntry.Open(), Encoding.UTF8, leaveOpen: false);
            await traversalWriter.WriteAsync("nope");
        }

        zipStream.Position = 0;
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("zip-bad"), "slug");
        var file = new StreamContent(zipStream);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/zip");
        content.Add(file, "file", "zip-bad.zip");

        var response = await client.PostAsync("/api/overlay/custom-presets", content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Given_CustomPresetSetting_When_OverlayRouteRequested_Then_CustomPresetRedirectPreservesQueryString()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        using var upload = BuildMultipartHtml("redirect-me", "<!DOCTYPE html><html><body>redirect-me</body></html>");
        var create = await client.PostAsync("/api/overlay/custom-presets", upload, TestContext.Current.CancellationToken);
        create.EnsureSuccessStatusCode();

        var setConfig = await client.PutAsJsonAsync(
            "/api/config/overlay.chat.preset",
            new { value = "custom:redirect-me" },
            TestContext.Current.CancellationToken);
        setConfig.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var redirectClient = CreateClient(app, allowAutoRedirect: false);
        var response = await redirectClient.GetAsync("/overlay/chat?foo=bar", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Be("/overlay/custom/redirect-me/index.html?foo=bar");
    }

    [Fact]
    public async Task Given_CustomPresetUpload_When_SlugInvalidOrFileTooLarge_Then_RequestIsRejected()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        using var invalidSlugUpload = BuildMultipartHtml("Bad Slug", "<html></html>");
        var invalidSlug = await client.PostAsync("/api/overlay/custom-presets", invalidSlugUpload, TestContext.Current.CancellationToken);
        invalidSlug.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var oversizedBytes = new byte[(5 * 1024 * 1024) + 1];
        await using var stream = new MemoryStream(oversizedBytes);
        using var oversized = new MultipartFormDataContent();
        oversized.Add(new StringContent("too-big"), "slug");
        var file = new StreamContent(stream);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/html");
        oversized.Add(file, "file", "too-big.html");

        var tooLarge = await client.PostAsync("/api/overlay/custom-presets", oversized, TestContext.Current.CancellationToken);
        tooLarge.StatusCode.Should().Be((HttpStatusCode)413);
    }

    private static MultipartFormDataContent BuildMultipartHtml(string slug, string html)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(slug), "slug");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(html));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/html");
        content.Add(file, "file", $"{slug}.html");
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
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Path"] = databasePath,
            ["Security:RootPath"] = Path.Combine(workingRoot, "security"),
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

        return new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
        })
        {
            BaseAddress = new Uri(address!)
        };
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

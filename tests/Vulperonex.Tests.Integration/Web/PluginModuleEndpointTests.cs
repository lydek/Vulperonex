using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Security;
using Vulperonex.Web;
using Xunit;

namespace Vulperonex.Tests.Integration.Web;

public sealed class PluginModuleEndpointTests
{
    [Fact]
    public async Task Given_DefaultModules_When_ListEndpointIsCalled_Then_ModuleGraphIsReturned()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);

        using var client = CreateClient(app);
        var response = await client.GetAsync("/api/plugins-modules", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var modules = await response.Content.ReadFromJsonAsync<List<PluginModuleDto>>(TestContext.Current.CancellationToken);
        modules.Should().NotBeNull();
        modules!.Should().Contain(item => item.Name == "member" && item.Dependents.Contains("checkin"));
        modules.Should().Contain(item => item.Name == "checkin" && item.Dependencies.Contains("member"));

        DeleteSqliteFiles(databasePath);
    }

    [Fact]
    public async Task Given_MemberModuleIsDisabled_When_ToggleEndpointIsCalled_Then_DependentModulesAreDisabledToo()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);

        using var client = CreateClient(app);
        var response = await client.PostAsJsonAsync(
            "/api/plugins-modules/member/toggle",
            new ToggleRequest(false),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ToggleResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.ChangedModules.Select(item => item.Name).Should().Contain(["member", "checkin", "lottery"]);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            (await settings.GetAsync(SystemSettingKey.ModuleEnabled("member"), true, TestContext.Current.CancellationToken)).Should().BeFalse();
            (await settings.GetAsync(SystemSettingKey.ModuleEnabled("checkin"), true, TestContext.Current.CancellationToken)).Should().BeFalse();
            (await settings.GetAsync(SystemSettingKey.ModuleEnabled("lottery"), true, TestContext.Current.CancellationToken)).Should().BeFalse();
        }

        DeleteSqliteFiles(databasePath);
    }

    [Fact]
    public async Task Given_WorkflowAndMemberModulesAreDisabled_When_CheckinIsEnabled_Then_DependenciesAreAutoEnabled()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            await settings.SetAsync(SystemSettingKey.ModuleEnabled("workflow"), false, "test", TestContext.Current.CancellationToken);
            await settings.SetAsync(SystemSettingKey.ModuleEnabled("member"), false, "test", TestContext.Current.CancellationToken);
            await settings.SetAsync(SystemSettingKey.ModuleEnabled("checkin"), false, "test", TestContext.Current.CancellationToken);
        }

        using var client = CreateClient(app);
        var response = await client.PostAsJsonAsync(
            "/api/plugins-modules/checkin/toggle",
            new ToggleRequest(true),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ToggleResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.ChangedModules.Select(item => item.Name).Should().Contain(["workflow", "member", "checkin"]);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            (await settings.GetAsync(SystemSettingKey.ModuleEnabled("workflow"), false, TestContext.Current.CancellationToken)).Should().BeTrue();
            (await settings.GetAsync(SystemSettingKey.ModuleEnabled("member"), false, TestContext.Current.CancellationToken)).Should().BeTrue();
            (await settings.GetAsync(SystemSettingKey.ModuleEnabled("checkin"), false, TestContext.Current.CancellationToken)).Should().BeTrue();
        }

        DeleteSqliteFiles(databasePath);
    }

    private static async Task<WebApplication> StartAppAsync(string databasePath)
    {
        var builder = VulperonexWebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development",
            },
            configureDefaultLoopbackPorts: false);

        var securityRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var configuration = new Dictionary<string, string?>
        {
            ["Database:Path"] = databasePath,
            ["Security:RootPath"] = securityRoot,
            ["Security:CsrfTokenPath"] = Path.Combine(securityRoot, ".admin-csrf-token"),
        };

        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddSingleton<IFileSystem, TestFileSystem>();
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://127.0.0.1:{GetAvailablePort()}");

        var app = VulperonexWebApplication.Build(builder);
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            .Single();

        var tokenProvider = app.Services.GetRequiredService<Vulperonex.Web.Security.AdminCsrfTokenProvider>();

        var client = new HttpClient { BaseAddress = new Uri(address!) };
        client.DefaultRequestHeaders.Add("X-Admin-Csrf", tokenProvider.Token);
        client.DefaultRequestHeaders.Add("Origin", address);
        client.DefaultRequestHeaders.Add("Referer", address);
        return client;
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void DeleteSqliteFiles(string databasePath)
    {
        foreach (var path in new[] { databasePath, $"{databasePath}-shm", $"{databasePath}-wal" })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class TestFileSystem : IFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);
        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
        public void WriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public void ApplyUserOnlyPermissions(string path) {}
    }

    private sealed record ToggleRequest(bool Enabled);

    private sealed class PluginModuleDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public List<string> Dependencies { get; set; } = [];
        public List<string> Dependents { get; set; } = [];
    }

    private sealed class ToggleResponse
    {
        public PluginModuleDto Module { get; set; } = new();
        public List<PluginModuleDto> ChangedModules { get; set; } = [];
    }
}

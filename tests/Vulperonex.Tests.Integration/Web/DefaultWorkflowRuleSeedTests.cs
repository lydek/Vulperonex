using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Dtos;
using Vulperonex.Web;
using Vulperonex.Web.Workflows;
using Xunit;

namespace Vulperonex.Tests.Integration.Web;

public sealed class DefaultWorkflowRuleSeedTests
{
    [Fact]
    public async Task Given_EmptyDb_When_AppStart_Then_SevenTypedRulesSeeded()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        try
        {
            await using var app = await StartAppAsync(databasePath);
            using var client = CreateClient(app);

            // 從 API 獲取所有 Rules
            var response = await client.GetAsync("/api/rules", TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var rules = await response.Content.ReadFromJsonAsync<List<WorkflowRuleSummaryDto>>(
                cancellationToken: TestContext.Current.CancellationToken);

            rules.Should().NotBeNull();
            // 應正確補種 7 個 default typed rules
            rules!.Count.Should().Be(7);

            // 驗證幾個具體的 typed rules 是否包含在其中
            rules.Should().Contain(r => r.Name.Contains("!checkin"));
            rules.Should().Contain(r => r.Name.Contains("Shoutout"));
            rules.Should().Contain(r => r.Name.Contains("Bits 100+"));
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    [Fact]
    public async Task Given_DbHasRules_When_AppStart_Then_SeedSkipped()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        try
        {
            // 1. 啟動並正確種入 7 個 rules
            await using (var app = await StartAppAsync(databasePath))
            {
                using var client = CreateClient(app);
                var response = await client.GetAsync("/api/rules", TestContext.Current.CancellationToken);
                var rules = await response.Content.ReadFromJsonAsync<List<WorkflowRuleSummaryDto>>(
                    cancellationToken: TestContext.Current.CancellationToken);
                rules!.Count.Should().Be(7);

                // 2. 手動刪除一筆，剩下 6 筆
                var location = $"/api/rules/{rules[0].Id}";
                var deleteResponse = await client.DeleteAsync(location, TestContext.Current.CancellationToken);
                deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
            }

            // 3. 第二次重啟 App，此時 DB 有 6 筆，應該 skip seeding，維持 6 筆！
            await using (var app = await StartAppAsync(databasePath))
            {
                using var client = CreateClient(app);
                var response = await client.GetAsync("/api/rules", TestContext.Current.CancellationToken);
                var rules = await response.Content.ReadFromJsonAsync<List<WorkflowRuleSummaryDto>>(
                    cancellationToken: TestContext.Current.CancellationToken);
                rules!.Count.Should().Be(6); // 沒有被 reseed 覆寫為 7 筆，完美符合 Idempotency！
            }
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    private static async Task<WebApplication> StartAppAsync(string databasePath)
    {
        var builder = VulperonexWebApplication.CreateBuilder(
            new WebApplicationOptions(),
            configureDefaultLoopbackPorts: false);

        builder.Configuration["Database:Path"] = databasePath;
        var securityRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        builder.Configuration["Security:RootPath"] = securityRoot;
        builder.Configuration["Security:CsrfTokenPath"] = Path.Combine(securityRoot, ".admin-csrf-token");

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

        var tokenProvider = app.Services.GetRequiredService<Vulperonex.Web.Security.AdminCsrfTokenProvider>();

        var client = new HttpClient
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
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void DeleteSqliteFiles(string databasePath)
    {
        try
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            var shm = $"{databasePath}-shm";
            if (File.Exists(shm)) File.Delete(shm);
            var wal = $"{databasePath}-wal";
            if (File.Exists(wal)) File.Delete(wal);
        }
        catch
        {
            // Ignore deletion errors in test cleanup
        }
    }
}

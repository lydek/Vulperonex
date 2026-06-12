using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Application.Members;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;
using Vulperonex.Infrastructure.Security;
using Vulperonex.Web;
using Vulperonex.Web.Configuration;
using Vulperonex.Web.Endpoints;
using Xunit;

namespace Vulperonex.Tests.Integration.Web;

public sealed class MemberMutationEndpointTests
{
    [Fact]
    public async Task Given_MemberExists_When_GetIsCalled_Then_ETagHeaderIsReturned()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);
        
        var ticks = DateTimeOffset.UtcNow.Ticks;
        await SeedMemberAsync(app, "member-1", 10, 2, ticks);

        using var scope = app.Services.CreateScope();
        var adminService = scope.ServiceProvider.GetRequiredService<IMemberAdminService>();
        var expectedEtag = await adminService.GetETagAsync("member-1", ticks, TestContext.Current.CancellationToken);

        using var client = CreateClient(app);
        var response = await client.GetAsync("/api/members/member-1", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        response.Headers.ETag!.Tag.Should().Be($"\"{expectedEtag}\"");

        DeleteSqliteFiles(databasePath);
    }

    [Fact]
    public async Task Given_MemberExists_When_AdjustLoyaltyIsCalledWithoutIfMatch_Then_428PreconditionRequiredIsReturned()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);
        await SeedMemberAsync(app, "member-1", 10, 2, DateTimeOffset.UtcNow.Ticks);

        using var client = CreateClient(app);
        var response = await client.PatchAsJsonAsync(
            "/api/members/member-1/loyalty",
            new AdjustLoyaltyRequest(20, 5, "Bonus points"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        DeleteSqliteFiles(databasePath);
    }

    [Fact]
    public async Task Given_MemberExists_When_AdjustLoyaltyIsCalledWithStaleIfMatch_Then_409ConflictIsReturned()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);
        var currentTicks = DateTimeOffset.UtcNow.Ticks;
        await SeedMemberAsync(app, "member-1", 10, 2, currentTicks);

        using var client = CreateClient(app);
        client.DefaultRequestHeaders.IfMatch.Add(new EntityTagHeaderValue("\"stale-etag-value\""));

        var response = await client.PatchAsJsonAsync(
            "/api/members/member-1/loyalty",
            new AdjustLoyaltyRequest(20, 5, "Bonus points"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        DeleteSqliteFiles(databasePath);
    }

    [Fact]
    public async Task Given_MemberExists_When_AdjustLoyaltyIsCalledWithCorrectIfMatch_Then_LoyaltyIsUpdatedAndAuditLogIsWritten()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);
        var currentTicks = DateTimeOffset.UtcNow.Ticks;
        await SeedMemberAsync(app, "member-1", 10, 2, currentTicks);

        using var scope = app.Services.CreateScope();
        var adminService = scope.ServiceProvider.GetRequiredService<IMemberAdminService>();
        var secureEtag = await adminService.GetETagAsync("member-1", currentTicks, TestContext.Current.CancellationToken);

        using var client = CreateClient(app);
        client.DefaultRequestHeaders.IfMatch.Add(new EntityTagHeaderValue($"\"{secureEtag}\""));

        var response = await client.PatchAsJsonAsync(
            "/api/members/member-1/loyalty",
            new AdjustLoyaltyRequest(20, 5, "Bonus points"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify database and audit log
        await using (var dbScope = app.Services.CreateAsyncScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            var member = await db.Members.FirstAsync(x => x.MemberId == "member-1", TestContext.Current.CancellationToken);
            member.TotalLoyalty.Should().Be(20);
            member.CheckInCount.Should().Be(5);
            member.UpdatedAtTicks.Should().NotBe(currentTicks);

            var auditLog = await db.MemberAuditLogs.FirstOrDefaultAsync(x => x.MemberId == "member-1", TestContext.Current.CancellationToken);
            auditLog.Should().NotBeNull();
            auditLog!.Operation.Should().Be("adjust_loyalty");
            auditLog.Reason.Should().Be("Bonus points");
        }

        DeleteSqliteFiles(databasePath);
    }

    [Fact]
    public async Task Given_MemberExists_When_ResetIsCalledWithCorrectIfMatch_Then_LoyaltyIsReset()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);
        var currentTicks = DateTimeOffset.UtcNow.Ticks;
        await SeedMemberAsync(app, "member-1", 10, 2, currentTicks);

        using var scope = app.Services.CreateScope();
        var adminService = scope.ServiceProvider.GetRequiredService<IMemberAdminService>();
        var secureEtag = await adminService.GetETagAsync("member-1", currentTicks, TestContext.Current.CancellationToken);

        using var client = CreateClient(app);
        client.DefaultRequestHeaders.IfMatch.Add(new EntityTagHeaderValue($"\"{secureEtag}\""));

        var response = await client.PostAsJsonAsync(
            "/api/members/member-1/reset",
            new ResetMemberRequest(true, true, "Admin reset"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var dbScope = app.Services.CreateAsyncScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            var member = await db.Members.FirstAsync(x => x.MemberId == "member-1", TestContext.Current.CancellationToken);
            member.TotalLoyalty.Should().Be(0);
            member.CheckInCount.Should().Be(0);

            var auditLog = await db.MemberAuditLogs.FirstOrDefaultAsync(x => x.MemberId == "member-1", TestContext.Current.CancellationToken);
            auditLog.Should().NotBeNull();
            auditLog!.Operation.Should().Be("reset");
            auditLog.Reason.Should().Be("Admin reset");
        }

        DeleteSqliteFiles(databasePath);
    }

    [Fact]
    public async Task Given_MemberExists_When_DeleteTokenIsRequested_Then_30SecondsDeleteTokenIsGenerated()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);
        await SeedMemberAsync(app, "member-delete-token", 10, 2, DateTimeOffset.UtcNow.Ticks);

        using var client = CreateClient(app);
        var tokenResponse = await client.PostAsync("/api/members/member-delete-token/delete-token", null, TestContext.Current.CancellationToken);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await tokenResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        var token = doc.RootElement.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        // Perform token delete
        var deleteResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/members/member-delete-token")
        {
            Content = JsonContent.Create(new DeleteMemberRequest(token, "Dangerous user"))
        }, TestContext.Current.CancellationToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            var exists = await db.Members.AnyAsync(x => x.MemberId == "member-delete-token", TestContext.Current.CancellationToken);
            exists.Should().BeFalse();

            // Audit logs follow an append-only contract, so deletes must not cascade.
            // After the member is removed, the related MemberAuditLogs rows must still remain in the database.
            var auditLog = await db.MemberAuditLogs.FirstOrDefaultAsync(x => x.MemberId == "member-delete-token" && x.Operation == "delete", TestContext.Current.CancellationToken);
            auditLog.Should().NotBeNull();
            auditLog!.Reason.Should().Be("Dangerous user");
        }

        DeleteSqliteFiles(databasePath);
    }

    [Fact]
    public async Task Given_MemberExists_When_DeleteWithInvalidTokenIsCalled_Then_400BadRequestIsReturned()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);
        await SeedMemberAsync(app, "member-bad-delete", 10, 2, DateTimeOffset.UtcNow.Ticks);

        using var client = CreateClient(app);
        var deleteResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/members/member-bad-delete")
        {
            Content = JsonContent.Create(new DeleteMemberRequest("invalid-token", "Remove anyway"))
        }, TestContext.Current.CancellationToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        DeleteSqliteFiles(databasePath);
    }

    [Fact]
    public async Task Given_MemberExists_When_DeleteWithoutTokenPayloadIsCalled_Then_400BadRequestIsReturned()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);
        await SeedMemberAsync(app, "member-missing-delete-token", 10, 2, DateTimeOffset.UtcNow.Ticks);

        using var client = CreateClient(app);
        var deleteResponse = await client.DeleteAsync("/api/members/member-missing-delete-token", TestContext.Current.CancellationToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            var exists = await db.Members.AnyAsync(x => x.MemberId == "member-missing-delete-token", TestContext.Current.CancellationToken);
            exists.Should().BeTrue();
        }

        DeleteSqliteFiles(databasePath);
    }

    [Fact]
    public async Task Given_MemberExists_When_DeletePayloadIsMalformedJson_Then_400BadRequestIsReturned()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);
        await SeedMemberAsync(app, "member-bad-json-delete", 10, 2, DateTimeOffset.UtcNow.Ticks);

        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/members/member-bad-json-delete")
        {
            Content = new StringContent("{\"token\":", System.Text.Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            var exists = await db.Members.AnyAsync(x => x.MemberId == "member-bad-json-delete", TestContext.Current.CancellationToken);
            exists.Should().BeTrue();
        }

        DeleteSqliteFiles(databasePath);
    }

    [Fact]
    public async Task Given_MemberExists_When_AuditLogsAreQueried_Then_PagedAuditLogsAreReturned()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using var app = await StartAppAsync(databasePath);
        await SeedMemberAsync(app, "member-audit", 10, 2, DateTimeOffset.UtcNow.Ticks);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var auditRepo = scope.ServiceProvider.GetRequiredService<IMemberAuditLogRepository>();
            for (int i = 0; i < 5; i++)
            {
                await auditRepo.AppendAsync(new MemberAuditLog
                {
                    Id = $"01F8MECH01000000000000000{i}",
                    MemberId = "member-audit",
                    ActorKind = "user",
                    Operation = "adjust_loyalty",
                    Reason = $"Reason {i}",
                    OccurredAt = DateTimeOffset.UtcNow.AddMinutes(i)
                }, TestContext.Current.CancellationToken);
            }
        }

        using var client = CreateClient(app);
        var response = await client.GetAsync("/api/members/member-audit/audit?limit=2&offset=1", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var logs = JsonSerializer.Deserialize<List<MemberAuditLogDto>>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        logs.Should().NotBeNull();
        logs!.Count.Should().Be(2);
        // Correct chronological order (ULID order or index descending)
        logs[0].Reason.Should().Be("Reason 3");
        logs[1].Reason.Should().Be("Reason 2");

        DeleteSqliteFiles(databasePath);
    }

    private static async Task SeedMemberAsync(WebApplication app, string id, int loyalty, int checkIn, long ticks)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        db.Members.Add(new MemberEntity
        {
            MemberId = id,
            TotalLoyalty = loyalty,
            CheckInCount = checkIn,
            UpdatedAtTicks = ticks
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<WebApplication> StartAppAsync(string databasePath)
    {
        var builder = VulperonexWebApplication.CreateBuilder(
            new Microsoft.AspNetCore.Builder.WebApplicationOptions
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

    private sealed class MemberAuditLogDto
    {
        public string Id { get; set; } = "";
        public string MemberId { get; set; } = "";
        public string Operation { get; set; } = "";
        public string Reason { get; set; } = "";
    }
}

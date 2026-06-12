using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Members;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Web;
using Xunit;

namespace Vulperonex.Tests.Integration.Workflows;

/// <summary>
/// End-to-end coverage for the chat → check-in → member overlay pipeline that the
/// per-layer unit tests cannot exercise. This is the exact incident class from the
/// 2026-05-27 outage: every layer worked in isolation, but a real twitch-platform
/// chat event never reached the member hub because of seed-rule/filter wiring.
///
/// Chain under test:
///   UserSentMessageEvent (platform=twitch, "!checkin")
///   → WorkflowEngineDispatcher → seeded "!checkin" rule (CommandName filter)
///   → TriggerCheckInActionExecutor → DB increment + audit log
///   → MemberCheckedInEvent → OverlayEventForwarder → /hubs/overlay/member push.
/// </summary>
public sealed class CheckInPipelineEndToEndTests
{
    private const string Platform = "twitch";
    private const string UserId = "e2e-viewer-1";
    private const string DisplayName = "E2E Viewer";

    [Fact]
    public async Task Given_TwitchChatCheckinCommand_When_Published_Then_MemberHubReceivesCardAndDbIncrements()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        try
        {
            await using var app = await StartAppAsync(databasePath);

            // Pre-bind the member so the executor's FindByIdentityAsync gate passes
            // deterministically. In production MemberModule auto-resolves on first
            // event, but that races the workflow dispatch; tests must not rely on
            // winning that race.
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var resolver = scope.ServiceProvider.GetRequiredService<IMemberResolver>();
                await resolver.ResolveMemberIdAsync(
                    PlatformIdentity.Create(Platform, UserId),
                    TestContext.Current.CancellationToken);
            }

            var memberCardReceived = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var connection = new HubConnectionBuilder()
                .WithUrl(new Uri(GetBaseAddress(app), "/hubs/overlay/member"))
                .Build();
            connection.On<JsonElement>("event", payload => memberCardReceived.TrySetResult(payload));
            await connection.StartAsync(TestContext.Current.CancellationToken);

            var bus = app.Services.GetRequiredService<IStreamEventBus>();
            await bus.PublishAsync(NewCheckinChatEvent(), TestContext.Current.CancellationToken);

            var completed = await Task.WhenAny(
                memberCardReceived.Task,
                Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            completed.Should().Be(
                memberCardReceived.Task,
                "a twitch-platform !checkin chat message must reach the member overlay hub end-to-end");

            var payload = await memberCardReceived.Task;
            payload.GetProperty("displayName").GetString().Should().Be(DisplayName);
            payload.GetProperty("checkInCount").GetInt32().Should().Be(1);
            payload.GetProperty("roundIndex").GetInt32().Should().Be(1);
            payload.GetProperty("stampSlotInRound").GetInt32().Should().Be(1);

            // Persistence really happened (twitch platform must NOT take the
            // simulation read-only path).
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var members = scope.ServiceProvider.GetRequiredService<IMemberQueryService>();
                var member = await members.FindByIdentityAsync(
                    PlatformIdentity.Create(Platform, UserId),
                    TestContext.Current.CancellationToken);
                member.Should().NotBeNull();
                member!.Loyalty.CheckInCount.Should().Be(1);

                var auditLogs = scope.ServiceProvider.GetRequiredService<IMemberAuditLogRepository>();
                var logs = await auditLogs.QueryAsync(member.MemberId, limit: 10, offset: 0, TestContext.Current.CancellationToken);
                logs.Should().Contain(log => log.Operation == "checkin" && log.ActorKind == "workflow");
            }
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    [Fact]
    public async Task Given_RepeatCheckinInSameWindow_When_Published_Then_CountDoesNotIncrementTwice()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        try
        {
            await using var app = await StartAppAsync(databasePath);

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var resolver = scope.ServiceProvider.GetRequiredService<IMemberResolver>();
                await resolver.ResolveMemberIdAsync(
                    PlatformIdentity.Create(Platform, UserId),
                    TestContext.Current.CancellationToken);
            }

            var bus = app.Services.GetRequiredService<IStreamEventBus>();

            await bus.PublishAsync(NewCheckinChatEvent(), TestContext.Current.CancellationToken);
            await WaitForCheckInCountAsync(app, expected: 1);

            // Second !checkin inside the same reset window: the repeat gate must
            // short-circuit before the increment.
            await bus.PublishAsync(NewCheckinChatEvent(), TestContext.Current.CancellationToken);
            await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);
            // Give the dispatcher's fire-and-forget execution a moment to settle.
            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var members = scope.ServiceProvider.GetRequiredService<IMemberQueryService>();
                var member = await members.FindByIdentityAsync(
                    PlatformIdentity.Create(Platform, UserId),
                    TestContext.Current.CancellationToken);
                member!.Loyalty.CheckInCount.Should().Be(1, "repeat check-in within the same reset window must not increment");
            }
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    [Fact]
    public async Task Given_RuleWithUnknownFilterKey_When_TwitchChatPublished_Then_RuleNeverFires()
    {
        // Regression for the 2026-05-27 incident: a rule whose trigger filter
        // carried an out-of-schema key (platform=simulation) silently blocked the
        // pipeline. With typed matchers, unknown keys must make the rule inert
        // rather than match-by-accident — and the assertion here documents that
        // contract at the integration level.
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        try
        {
            await using var app = await StartAppAsync(databasePath);

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var resolver = scope.ServiceProvider.GetRequiredService<IMemberResolver>();
                await resolver.ResolveMemberIdAsync(
                    PlatformIdentity.Create(Platform, UserId),
                    TestContext.Current.CancellationToken);

                // Disable the seeded !checkin rule and replace it with the legacy
                // bad-filter shape so only the bad rule could possibly fire.
                var repository = scope.ServiceProvider.GetRequiredService<Vulperonex.Application.Workflows.IWorkflowRuleRepository>();
                var queries = scope.ServiceProvider.GetRequiredService<Vulperonex.Application.Workflows.IWorkflowRuleQueryService>();
                foreach (var existing in await queries.ListAsync(TestContext.Current.CancellationToken))
                {
                    await repository.DeleteAsync(existing.Id, TestContext.Current.CancellationToken);
                }

                await repository.AddAsync(new Vulperonex.Application.Workflows.WorkflowRule
                {
                    Id = UlidGenerator.NewUlidString(),
                    Name = "Legacy bad rule - platform filter",
                    EventTypeKey = "user.message",
                    IsEnabled = true,
                    Priority = 100,
                    Trigger = new Vulperonex.Application.Workflows.WorkflowTrigger(
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["platform"] = "simulation"
                        }),
                    Actions =
                    [
                        new Vulperonex.Application.Workflows.Actions.TriggerCheckInAction
                        {
                            UserId = "{Member.UserId}"
                        }
                    ]
                }, TestContext.Current.CancellationToken);
            }

            var bus = app.Services.GetRequiredService<IStreamEventBus>();
            await bus.PublishAsync(NewCheckinChatEvent(), TestContext.Current.CancellationToken);
            await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var members = scope.ServiceProvider.GetRequiredService<IMemberQueryService>();
                var member = await members.FindByIdentityAsync(
                    PlatformIdentity.Create(Platform, UserId),
                    TestContext.Current.CancellationToken);
                member!.Loyalty.CheckInCount.Should().Be(0, "a rule with an unknown filter key must be inert, never accidentally firing");
            }
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    [Fact]
    public async Task Given_DbWithOnlyLegacyBadRule_When_AppRestarts_Then_SeedSkipsAndPipelineStaysInert()
    {
        // Cross-restart data-compatibility scenario from the 2026-05-27 incident:
        // an operator-era DB holds a single broken rule (out-of-schema filter key).
        // On restart the seeder must respect operator data (no re-seed), which
        // means the pipeline stays dead — this test pins both halves of that
        // contract so the behaviour is a documented decision, not an accident.
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        try
        {
            // Session 1: replace all seeded rules with the legacy bad rule.
            await using (var app = await StartAppAsync(databasePath))
            {
                await using var scope = app.Services.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<Vulperonex.Application.Workflows.IWorkflowRuleRepository>();
                var queries = scope.ServiceProvider.GetRequiredService<Vulperonex.Application.Workflows.IWorkflowRuleQueryService>();
                foreach (var existing in await queries.ListAsync(TestContext.Current.CancellationToken))
                {
                    await repository.DeleteAsync(existing.Id, TestContext.Current.CancellationToken);
                }

                await repository.AddAsync(new Vulperonex.Application.Workflows.WorkflowRule
                {
                    Id = UlidGenerator.NewUlidString(),
                    Name = "Legacy bad rule - platform filter",
                    EventTypeKey = "user.message",
                    IsEnabled = true,
                    Priority = 100,
                    Trigger = new Vulperonex.Application.Workflows.WorkflowTrigger(
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["platform"] = "simulation"
                        }),
                    Actions =
                    [
                        new Vulperonex.Application.Workflows.Actions.TriggerCheckInAction
                        {
                            UserId = "{Member.UserId}"
                        }
                    ]
                }, TestContext.Current.CancellationToken);
            }

            // Session 2: restart against the same DB.
            await using (var app = await StartAppAsync(databasePath))
            {
                await using (var scope = app.Services.CreateAsyncScope())
                {
                    var queries = scope.ServiceProvider.GetRequiredService<Vulperonex.Application.Workflows.IWorkflowRuleQueryService>();
                    var rules = await queries.ListAsync(TestContext.Current.CancellationToken);
                    rules.Should().HaveCount(1, "seeding must skip when operator-managed rules exist, even broken ones");
                    rules[0].Name.Should().Contain("Legacy bad rule");

                    var resolver = scope.ServiceProvider.GetRequiredService<IMemberResolver>();
                    await resolver.ResolveMemberIdAsync(
                        PlatformIdentity.Create(Platform, UserId),
                        TestContext.Current.CancellationToken);
                }

                var bus = app.Services.GetRequiredService<IStreamEventBus>();
                await bus.PublishAsync(NewCheckinChatEvent(), TestContext.Current.CancellationToken);
                await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

                await using (var scope = app.Services.CreateAsyncScope())
                {
                    var members = scope.ServiceProvider.GetRequiredService<IMemberQueryService>();
                    var member = await members.FindByIdentityAsync(
                        PlatformIdentity.Create(Platform, UserId),
                        TestContext.Current.CancellationToken);
                    member!.Loyalty.CheckInCount.Should().Be(
                        0,
                        "the legacy rule's unknown filter key keeps it inert and the seeder must not have added a working rule beside it");
                }
            }
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    private static UserSentMessageEvent NewCheckinChatEvent()
    {
        return new UserSentMessageEvent
        {
            Platform = Platform,
            User = new StreamUser(Platform, UserId, DisplayName, StreamRole.None, "e2e_viewer_1"),
            MessageText = "!checkin"
        };
    }

    private static async Task WaitForCheckInCountAsync(WebApplication app, int expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var members = scope.ServiceProvider.GetRequiredService<IMemberQueryService>();
            var member = await members.FindByIdentityAsync(
                PlatformIdentity.Create(Platform, UserId),
                TestContext.Current.CancellationToken);
            if (member?.Loyalty.CheckInCount >= expected)
            {
                return;
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Check-in count did not reach {expected} within 10 seconds.");
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

    private static Uri GetBaseAddress(WebApplication app)
    {
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            .Single();
        return new Uri(address!);
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
        try
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists($"{databasePath}-shm")) File.Delete($"{databasePath}-shm");
            if (File.Exists($"{databasePath}-wal")) File.Delete($"{databasePath}-wal");
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }
}

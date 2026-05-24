using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Collections.Concurrent;
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
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;
using Vulperonex.Web;
using Xunit;

namespace Vulperonex.Tests.Integration.Web;

public sealed class SignalRHubTests
{
    [Fact]
    public async Task Given_OverlayChatHub_When_ChatIsSimulated_Then_EventArrivesWithinFiveSeconds()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        var message = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/chat"))
            .Build();

        connection.On<JsonElement>("event", payload => message.TrySetResult(payload));
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var startedAt = DateTimeOffset.UtcNow;
        var response = await client.PostAsJsonAsync(
            "/api/simulate/chat",
            new { message = "signalr smoke" },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var completed = await Task.WhenAny(message.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        completed.Should().Be(message.Task);
        var elapsed = DateTimeOffset.UtcNow - startedAt;
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        var payload = await message.Task;
        payload.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        payload.GetProperty("eventId").GetString().Should().NotBeNullOrWhiteSpace();
        payload.GetProperty("segments")[0].GetProperty("value").GetString().Should().Be("signalr smoke");
    }

    [Fact]
    public async Task Given_EventsHub_When_ChatIsSimulated_Then_EnvelopeContainsDiscriminator()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        var message = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/events"))
            .Build();

        connection.On<JsonElement>("event", payload => message.TrySetResult(payload));
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync(
            "/api/simulate/chat",
            new { message = "management hub" },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var completed = await Task.WhenAny(message.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        completed.Should().Be(message.Task);
        var payload = await message.Task;
        payload.GetProperty("type").GetString().Should().Be("user.message");
        payload.GetProperty("eventId").GetString().Should().NotBeNullOrWhiteSpace();
        payload.TryGetProperty("$type", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Given_MemberAndDisplayCacheExist_When_ChatIsSimulated_Then_OverlayChatPayloadIncludesMemberSnapshot()
    {
        await using var app = await StartAppAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            context.Members.Add(new MemberEntity
            {
                MemberId = "member-1",
                CheckInCount = 7,
                TotalLoyalty = 21,
            });
            context.PlatformIdentities.Add(new PlatformIdentityEntity
            {
                MemberId = "member-1",
                Platform = "simulation",
                PlatformUserId = "member-chat-user",
            });
            context.PlatformUserDisplayInfo.Add(new PlatformUserDisplayInfoEntity
            {
                Platform = "simulation",
                PlatformUserId = "member-chat-user",
                AvatarUrl = "https://cdn.example/avatar.png",
                ColorHex = "#12abef",
                BadgesJson = "[\"subscriber/1\"]",
                FetchedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = CreateClient(app);
        var message = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/chat"))
            .Build();

        connection.On<JsonElement>("event", payload => message.TrySetResult(payload));
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync(
            "/api/simulate/chat",
            new { platformUserId = "member-chat-user", displayName = "Member User", message = "snapshot me" },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await message.Task.WaitAsync(TestContext.Current.CancellationToken);
        payload.GetProperty("colorHex").GetString().Should().Be("#12abef");
        payload.GetProperty("badges")[0].GetString().Should().Be("subscriber/1");
        payload.GetProperty("memberSnapshot").GetProperty("displayName").GetString().Should().Be("Member User");
        payload.GetProperty("memberSnapshot").GetProperty("avatarUrl").GetString().Should().Be("https://cdn.example/avatar.png");
        payload.GetProperty("memberSnapshot").GetProperty("checkInCount").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task Given_ConfigEndpointWritesSetting_When_EventsHubConnected_Then_SystemConfigChangedIsBroadcast()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        var message = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/events"))
            .Build();

        connection.On<JsonElement>("event", payload =>
        {
            if (payload.TryGetProperty("type", out var type) && type.GetString() == "system.config_changed")
            {
                message.TrySetResult(payload);
            }
        });
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var response = await client.PutAsJsonAsync(
            "/api/config/overlay.chat.show_member_card",
            new { value = "true" },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await message.Task.WaitAsync(TestContext.Current.CancellationToken);
        payload.GetProperty("platform").GetString().Should().Be("system");
        payload.GetProperty("key").GetString().Should().Be("overlay.chat.show_member_card");
        payload.GetProperty("value").GetString().Should().Be("true");
    }

    [Fact]
    public async Task Given_OverlayAlertsHub_When_FollowIsSimulated_Then_AlertArrives()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        var message = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/alerts"))
            .Build();

        connection.On<JsonElement>("event", payload => message.TrySetResult(payload));
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync(
            "/api/simulate/follow",
            new { displayName = "Follower" },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var completed = await Task.WhenAny(message.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        completed.Should().Be(message.Task);
        var payload = await message.Task;
        payload.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        payload.GetProperty("eventType").GetString().Should().Be("followed");
        payload.GetProperty("displayName").GetString().Should().Be("Follower");
    }

    [Fact]
    public async Task Given_OverlayEffectsHub_When_EffectEmitted_Then_StrongTypedPayloadArrives()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        var message = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/effects"))
            .Build();

        connection.On<JsonElement>("event", payload => message.TrySetResult(payload));
        await connection.StartAsync(TestContext.Current.CancellationToken);
        var emitter = app.Services.GetRequiredService<IOverlayEffectEmitter>();

        await emitter.EmitAsync(
            new OverlayEffectPayload(1, "evt-1", DateTimeOffset.UnixEpoch, "sparkle", 1_500),
            TestContext.Current.CancellationToken);

        var completed = await Task.WhenAny(message.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        completed.Should().Be(message.Task);
        var payload = await message.Task;
        payload.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("schemaVersion", "eventId", "timestamp", "effectId", "durationMs");
        payload.GetProperty("effectId").GetString().Should().Be("sparkle");
        payload.TryGetProperty("eventType", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Given_OverlayWidgetsHub_When_WidgetEmitted_Then_StrongTypedPayloadArrivesAndReplays()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        var liveMessage = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var liveConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/widgets"))
            .Build();

        liveConnection.On<JsonElement>("event", payload => liveMessage.TrySetResult(payload));
        await liveConnection.StartAsync(TestContext.Current.CancellationToken);
        var emitter = app.Services.GetRequiredService<IOverlayWidgetEmitter>();

        await emitter.EmitAsync(
            new OverlayWidgetPayload(1, "evt-1", DateTimeOffset.UnixEpoch, "channel_point", "alerts", "Redeemed", "success", 5_000),
            TestContext.Current.CancellationToken);

        var liveCompleted = await Task.WhenAny(liveMessage.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        liveCompleted.Should().Be(liveMessage.Task);
        var livePayload = await liveMessage.Task;
        livePayload.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("schemaVersion", "eventId", "timestamp", "widgetType", "overlayTarget", "displayText", "severity", "durationMs");
        livePayload.GetProperty("widgetType").GetString().Should().Be("channel_point");
        livePayload.TryGetProperty("payload", out _).Should().BeFalse();

        var replayed = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var replayConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/widgets"))
            .Build();
        replayConnection.On<JsonElement>("event", payload => replayed.TrySetResult(payload));
        await replayConnection.StartAsync(TestContext.Current.CancellationToken);

        var replayCompleted = await Task.WhenAny(replayed.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        replayCompleted.Should().Be(replayed.Task);
        (await replayed.Task).GetProperty("displayText").GetString().Should().Be("Redeemed");
    }

    [Fact]
    public async Task Given_OverlayChatHub_When_MultipleClientsReceiveBurst_Then_AllEventsArriveWithinFiveSeconds()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        var receivedByMessage = new ConcurrentDictionary<string, int>();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connections = new List<HubConnection>();

        try
        {
            for (var index = 0; index < 5; index++)
            {
                var connection = new HubConnectionBuilder()
                    .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/chat"))
                    .Build();
                connection.On<JsonElement>("event", payload =>
                {
                    var value = payload.GetProperty("segments")[0].GetProperty("value").GetString();
                    if (value is not null)
                    {
                        receivedByMessage.AddOrUpdate(value, 1, (_, count) => count + 1);
                    }

                    if (receivedByMessage.Values.Sum() >= 50)
                    {
                        received.TrySetResult();
                    }
                });
                await connection.StartAsync(TestContext.Current.CancellationToken);
                connections.Add(connection);
            }

            for (var index = 0; index < 10; index++)
            {
                var message = $"load-{index}";
                receivedByMessage[message] = 0;
                var response = await client.PostAsJsonAsync(
                    "/api/simulate/chat",
                    new { message },
                    TestContext.Current.CancellationToken);
                response.EnsureSuccessStatusCode();
            }

            var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

            completed.Should().Be(received.Task);
        }
        finally
        {
            foreach (var connection in connections)
            {
                await connection.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Given_PriorChatHistory_When_OverlayChatHubConnects_Then_HistoryReplayedToCaller()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var firstResponse = await client.PostAsJsonAsync(
            "/api/simulate/chat",
            new { message = "history-1" },
            TestContext.Current.CancellationToken);
        firstResponse.EnsureSuccessStatusCode();
        var secondResponse = await client.PostAsJsonAsync(
            "/api/simulate/chat",
            new { message = "history-2" },
            TestContext.Current.CancellationToken);
        secondResponse.EnsureSuccessStatusCode();
        await app.Services.GetRequiredService<IStreamEventBus>()
            .WaitForIdleAsync(TestContext.Current.CancellationToken);

        var received = new List<string>();
        var allReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/chat"))
            .Build();
        connection.On<JsonElement>("event", payload =>
        {
            received.Add(payload.GetProperty("segments")[0].GetProperty("value").GetString()!);
            if (received.Count >= 2)
            {
                allReceived.TrySetResult();
            }
        });

        await connection.StartAsync(TestContext.Current.CancellationToken);
        var completed = await Task.WhenAny(allReceived.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        completed.Should().Be(allReceived.Task);
        received.Should().Equal("history-1", "history-2");
    }

    [Fact]
    public async Task Given_PriorAlertHistory_When_OverlayAlertsHubConnects_Then_ReplayedFlagIsTrue()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var followResponse = await client.PostAsJsonAsync(
            "/api/simulate/follow",
            new { displayName = "ReplayedFollower" },
            TestContext.Current.CancellationToken);
        followResponse.EnsureSuccessStatusCode();
        await app.Services.GetRequiredService<IStreamEventBus>()
            .WaitForIdleAsync(TestContext.Current.CancellationToken);

        var message = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/alerts"))
            .Build();
        connection.On<JsonElement>("event", payload => message.TrySetResult(payload));

        await connection.StartAsync(TestContext.Current.CancellationToken);
        var completed = await Task.WhenAny(message.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        completed.Should().Be(message.Task);
        var payload = await message.Task;
        payload.GetProperty("replayed").GetBoolean().Should().BeTrue();
        payload.GetProperty("displayName").GetString().Should().Be("ReplayedFollower");
    }

    [Fact]
    public async Task Given_HistoryExists_When_ClearEndpointCalled_Then_HubBroadcastsClearedAndNextConnectGetsNothing()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var seedResponse = await client.PostAsJsonAsync(
            "/api/simulate/chat",
            new { message = "to-be-cleared" },
            TestContext.Current.CancellationToken);
        seedResponse.EnsureSuccessStatusCode();
        await app.Services.GetRequiredService<IStreamEventBus>()
            .WaitForIdleAsync(TestContext.Current.CancellationToken);

        var cleared = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var listener = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/chat"))
            .Build();
        listener.On<JsonElement>("cleared", payload => cleared.TrySetResult(payload));
        await listener.StartAsync(TestContext.Current.CancellationToken);

        var clearResponse = await client.DeleteAsync("/api/overlay/chat/messages", TestContext.Current.CancellationToken);
        clearResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        var clearedCompleted = await Task.WhenAny(cleared.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        clearedCompleted.Should().Be(cleared.Task);
        (await cleared.Task).GetProperty("hubName").GetString().Should().Be("chat");

        var received = new List<JsonElement>();
        await using var freshConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/chat"))
            .Build();
        freshConnection.On<JsonElement>("event", payload => received.Add(payload));
        await freshConnection.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        received.Should().BeEmpty();
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var builder = VulperonexWebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development",
            },
            configureDefaultLoopbackPorts: false);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Path"] = databasePath,
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

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            .Single();

        return new HttpClient { BaseAddress = new Uri(address!) };
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

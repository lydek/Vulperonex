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
using Vulperonex.Infrastructure.Data;
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
    public async Task Given_OverlayChatHub_When_MultipleClientsReceiveBurst_Then_P95LatencyStaysUnderOneSecond()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        var startedByMessage = new ConcurrentDictionary<string, DateTimeOffset>();
        var latencies = new ConcurrentBag<TimeSpan>();
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
                    if (value is not null && startedByMessage.TryGetValue(value, out var startedAt))
                    {
                        latencies.Add(DateTimeOffset.UtcNow - startedAt);
                    }

                    if (latencies.Count >= 50)
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
                startedByMessage[message] = DateTimeOffset.UtcNow;
                var response = await client.PostAsJsonAsync(
                    "/api/simulate/chat",
                    new { message },
                    TestContext.Current.CancellationToken);
                response.EnsureSuccessStatusCode();
            }

            var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

            completed.Should().Be(received.Task);
            var p95 = latencies
                .OrderBy(latency => latency)
                .ElementAt((int)Math.Ceiling(latencies.Count * 0.95) - 1);
            p95.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }
        finally
        {
            foreach (var connection in connections)
            {
                await connection.DisposeAsync();
            }
        }
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

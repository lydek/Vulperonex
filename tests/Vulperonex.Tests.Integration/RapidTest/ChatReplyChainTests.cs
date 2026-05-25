using System.Net.Http.Json;
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
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Chat;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Web;
using Xunit;

namespace Vulperonex.Tests.Integration.RapidTest;

public sealed class ChatReplyChainTests
{
    [Fact]
    public async Task Given_WorkflowRuleAndOverlayConnected_When_ChatSimulated_Then_EchoPayloadReceivedBySignalR()
    {
        // 1. Start App with custom LoopbackChatSender registered
        var sender = new LoopbackChatSender("loopback");
        await using var app = await StartAppWithSenderAsync(sender);
        using var client = CreateClient(app);

        // 2. Load the minimal rule from JSON file and POST it
        var solutionDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (solutionDir != null && !File.Exists(Path.Combine(solutionDir.FullName, "Vulperonex.sln")))
        {
            solutionDir = solutionDir.Parent;
        }
        solutionDir.Should().NotBeNull("Solution root directory must be found");

        var ruleJsonPath = Path.Combine(solutionDir!.FullName, "docs", "phases", "phase-5_5-rapid-test", "examples", "rule-chat-echo.json");
        var ruleJson = await File.ReadAllTextAsync(ruleJsonPath, TestContext.Current.CancellationToken);
        var rulePayload = JsonSerializer.Deserialize<JsonElement>(ruleJson);

        var createRuleResponse = await client.PostAsJsonAsync("/api/rules", rulePayload, TestContext.Current.CancellationToken);
        createRuleResponse.EnsureSuccessStatusCode();

        // 3. Connect SignalR client to Overlay Chat Hub
        var echoMessageReceived = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/overlay/chat"))
            .Build();

        connection.On<JsonElement>("event", payload =>
        {
            var segments = payload.GetProperty("segments");
            if (segments.ValueKind == JsonValueKind.Array && segments.GetArrayLength() > 0)
            {
                var text = segments[0].GetProperty("value").GetString();
                if (text != null && text.StartsWith("Echo:", StringComparison.OrdinalIgnoreCase))
                {
                    echoMessageReceived.TrySetResult(payload);
                }
            }
        });
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // 4. Simulate chat input to trigger rule
        var simulateResponse = await client.PostAsJsonAsync(
            "/api/simulate/chat",
            new { message = "hello" },
            TestContext.Current.CancellationToken);
        simulateResponse.EnsureSuccessStatusCode();

        // 5. Assert SignalR client receives the Echo within 5 seconds
        var completed = await Task.WhenAny(echoMessageReceived.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        completed.Should().Be(echoMessageReceived.Task, "SignalR client should receive the echo reply within 5 seconds");

        var payload = await echoMessageReceived.Task;
        payload.GetProperty("segments")[0].GetProperty("value").GetString().Should().Be("Echo: hello");

        // 6. Assert outbox was triggered and contains the echo item
        var outbox = app.Services.GetRequiredService<IChatOutbox>();
        var outboxSnapshot = await outbox.SnapshotAsync(TestContext.Current.CancellationToken);
        outboxSnapshot.Should().ContainSingle(item => item.Message == "Echo: hello");
    }

    private sealed class LoopbackChatSender(string platform) : IPlatformChatSender
    {
        public string Platform { get; } = platform;
        public IStreamEventBus? EventBus { get; set; }

        public async Task SendAsync(string message, CancellationToken cancellationToken = default)
        {
            if (EventBus != null)
            {
                // Publish back to the bus so it routes to the overlay chat hub just like a real platform reply!
                await EventBus.PublishAsync(new UserSentMessageEvent
                {
                    Platform = Platform,
                    User = new Vulperonex.Domain.StreamUser(Platform, "bot", "ChatBot"),
                    MessageText = message
                }, cancellationToken);
            }
        }
    }

    private static async Task<WebApplication> StartAppWithSenderAsync(LoopbackChatSender sender)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
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

        // Register custom LoopbackChatSender and let it get access to IStreamEventBus once built
        builder.Services.AddSingleton<IPlatformChatSender>(sender);

        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://127.0.0.1:{GetAvailablePort()}");

        var app = VulperonexWebApplication.Build(builder);

        sender.EventBus = app.Services.GetRequiredService<IStreamEventBus>();

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
}

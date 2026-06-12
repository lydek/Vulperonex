using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using Vulperonex.Adapters.Abstractions;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Adapters.Twitch.Display;
using Vulperonex.Adapters.Twitch.Irc;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Domain.Events;

namespace Vulperonex.Adapters.Twitch.Irc;

/// <summary>
/// Live Twitch chat ingestion via IRC (TwitchLib.Client). Mirrors omni-commander
/// TwitchChatService: receive chat, build the adapter's <see cref="TwitchIrcMessage"/>,
/// and route through <see cref="TwitchAdapter.IngestChatAsync"/> so the existing
/// parser + display-cache pipeline is reused.
/// </summary>
public sealed class TwitchIrcChatSource(
    TwitchAdapter adapter,
    IStreamEventBus eventBus,
    IServiceScopeFactory scopeFactory,
    ILogger<TwitchIrcChatSource> logger) : IPlatformChatSender
{
    private TwitchClient? _client;
    private string? _channelLogin;

    public string Platform => "twitch";

    public async Task ConnectAsync(string channelLogin, string accessToken, CancellationToken cancellationToken)
    {
        // A TwitchClient cannot be re-initialized after use, and a reconnect
        // must carry a freshly refreshed access token (user tokens expire
        // after ~4h), so every connect builds a new client instance.
        await DisconnectAsync().ConfigureAwait(false);

        var client = new TwitchClient();
        client.OnMessageReceived += OnMessageReceivedAsync;
        client.OnConnected += OnConnectedAsync;
        client.OnDisconnected += OnDisconnectedAsync;
        client.OnIncorrectLogin += OnIncorrectLoginAsync;

        _channelLogin = channelLogin;
        client.Initialize(new ConnectionCredentials(channelLogin, accessToken), channelLogin);
        _client = client;
        await client.ConnectAsync().ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        var client = Interlocked.Exchange(ref _client, null);
        if (client is null)
        {
            return;
        }

        // Unhook before disconnecting so the orchestrator's reconnect
        // supervision is not re-triggered by our own teardown.
        client.OnMessageReceived -= OnMessageReceivedAsync;
        client.OnConnected -= OnConnectedAsync;
        client.OnDisconnected -= OnDisconnectedAsync;
        client.OnIncorrectLogin -= OnIncorrectLoginAsync;

        if (client.IsConnected)
        {
            await client.DisconnectAsync().ConfigureAwait(false);
        }
    }

    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = _client;
        if (client is null || !client.IsConnected || string.IsNullOrWhiteSpace(_channelLogin))
        {
            throw new InvalidOperationException("Twitch IRC is not connected.");
        }

        return client.SendMessageAsync(_channelLogin, message);
    }

    private async Task OnMessageReceivedAsync(object? sender, OnMessageReceivedArgs e)
    {
        var chat = e.ChatMessage;
        var tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["msg-id"] = string.IsNullOrEmpty(chat.Id) ? Guid.NewGuid().ToString() : chat.Id,
            ["user-id"] = chat.UserId ?? string.Empty,
            ["display-name"] = string.IsNullOrEmpty(chat.DisplayName) ? chat.Username : chat.DisplayName,
        };

        if (!string.IsNullOrWhiteSpace(chat.HexColor))
        {
            tags["color"] = chat.HexColor;
        }

        if (chat.Badges is { Count: > 0 })
        {
            tags["badges"] = string.Join(",", chat.Badges.Select(b => $"{b.Key}/{b.Value}"));
        }

        if (chat.Bits > 0)
        {
            tags["bits"] = chat.Bits.ToString();
        }

        if (chat.EmoteSet?.Emotes is { Count: > 0 })
        {
            var groupById = chat.EmoteSet.Emotes
                .GroupBy(e => e.Id)
                .Select(g => $"{g.Key}:{string.Join(",", g.Select(e => $"{e.StartIndex}-{e.EndIndex}"))}");
            tags["emotes"] = string.Join("/", groupById);
        }

        var message = new TwitchIrcMessage(tags, chat.Username, chat.Channel, chat.Message);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var cache = scope.ServiceProvider.GetRequiredService<IPlatformUserInfoCache>();
            await adapter.IngestChatAsync(message, new TwitchDisplayCacheUpdater(cache));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to ingest Twitch IRC chat message.");
        }
    }

    private async Task OnConnectedAsync(object? sender, OnConnectedEventArgs e)
    {
        logger.LogInformation("Twitch IRC connected.");
        await PublishConnectionAsync(connected: true, reason: null);
    }

    private async Task OnDisconnectedAsync(object? sender, OnDisconnectedArgs e)
    {
        logger.LogWarning("Twitch IRC disconnected.");
        await PublishConnectionAsync(connected: false, reason: "irc_disconnected");
    }

    private async Task OnIncorrectLoginAsync(object? sender, OnIncorrectLoginArgs e)
    {
        // TwitchLib fires OnIncorrectLogin when the IRC AUTHFAIL handshake
        // rejects the bot account's access token (expired/missing chat scope).
        // Surface as auth_failed so the UI prompts re-grant rather than
        // showing a generic "disconnected" state.
        logger.LogWarning("Twitch IRC rejected the access token; operator likely needs to re-grant chat:read/chat:edit scopes.");
        await PublishConnectionAsync(connected: false, reason: "auth_failed");
    }

    private async Task PublishConnectionAsync(bool connected, string? reason)
    {
        try
        {
            await eventBus.PublishAsync(new PlatformConnectionChangedEvent
            {
                Platform = "twitch",
                IsConnected = connected,
                Reason = reason,
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish Twitch IRC connection state.");
        }
    }
}

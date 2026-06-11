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
    private readonly TwitchClient _client = new();
    private int _wired;
    private string? _channelLogin;

    public string Platform => "twitch";

    public Task ConnectAsync(string channelLogin, string accessToken, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _wired, 1) == 0)
        {
            _client.OnMessageReceived += OnMessageReceivedAsync;
            _client.OnConnected += OnConnectedAsync;
            _client.OnDisconnected += OnDisconnectedAsync;
            _client.OnIncorrectLogin += OnIncorrectLoginAsync;
        }

        _channelLogin = channelLogin;
        _client.Initialize(new ConnectionCredentials(channelLogin, accessToken), channelLogin);
        return _client.ConnectAsync();
    }

    public Task DisconnectAsync()
    {
        return _client.IsConnected ? _client.DisconnectAsync() : Task.CompletedTask;
    }

    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_client.IsConnected || string.IsNullOrWhiteSpace(_channelLogin))
        {
            throw new InvalidOperationException("Twitch IRC is not connected.");
        }

        return _client.SendMessageAsync(_channelLogin, message);
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

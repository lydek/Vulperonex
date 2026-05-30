using Vulperonex.Adapters.Abstractions;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Adapters.Twitch.Display;
using Vulperonex.Adapters.Twitch.EventSub;
using Vulperonex.Adapters.Twitch.Irc;
using Vulperonex.Adapters.Twitch.Reconnect;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.EventTypes;
using Vulperonex.Domain.Events;

namespace Vulperonex.Adapters.Twitch;

public sealed class TwitchAdapter(
    IStreamEventBus eventBus,
    IStreamEventTypeRegistry eventTypeRegistry,
    TwitchDisplayCacheUpdater? displayCacheUpdater = null,
    EventSubDedupCache? eventSubDedupCache = null,
    ReconnectBackoffPolicy? reconnectBackoffPolicy = null,
    PkceStateStore? pkceStateStore = null) : IStreamEventSource
{
    private static readonly StreamEventTypeMetadata[] SupportedEventTypes =
    [
        new(StreamEventKeys.UserSentMessage, "Twitch chat message"),
        new(StreamEventKeys.UserFollowed, "Twitch follow"),
        new(StreamEventKeys.UserDonated, "Twitch bits donation"),
        new(StreamEventKeys.UserSubscribed, "Twitch subscription"),
        new(StreamEventKeys.UserGiftedSubscription, "Twitch gifted subscription"),
        new(StreamEventKeys.ChannelRaided, "Twitch raid"),
        new(StreamEventKeys.RewardRedeemed, "Twitch reward redemption"),
        new(StreamEventKeys.PlatformConnectionChanged, "Twitch connection state changed", IsSystemEvent: true),
    ];

    private int _started;

    public TimeSpan NextReconnectDelay(int attempt)
    {
        return (reconnectBackoffPolicy ?? new ReconnectBackoffPolicy()).GetDelay(attempt);
    }

    internal string CreateOAuthState()
    {
        return GetPkceStateStore().Create();
    }

    internal bool ValidateOAuthCallback(OAuthCallbackRequest request, int callbackPort)
    {
        return new OAuthCallbackValidator(
                callbackPort,
                GetPkceStateStore())
            .IsValid(request);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return Task.CompletedTask;
        }

        foreach (var metadata in SupportedEventTypes)
        {
            eventTypeRegistry.Register(metadata);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _started, 0);
        return Task.CompletedTask;
    }

    internal Task PublishMockPayloadAsync(TwitchMockPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        EnsureStarted();
        return PublishMappedEventAsync(TwitchEventMapper.Map(payload), displayCacheUpdater, cancellationToken);
    }

    /// <summary>
    /// Ingest a live Twitch chat message (IRC). <paramref name="displayCacheOverride"/>
    /// lets a background host supply a scoped <see cref="TwitchDisplayCacheUpdater"/>
    /// (the singleton adapter cannot capture the scoped user-info cache itself).
    /// </summary>
    public Task IngestChatAsync(
        TwitchIrcMessage message,
        TwitchDisplayCacheUpdater? displayCacheOverride = null,
        CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        return PublishIrcMessageAsync(message, displayCacheOverride ?? displayCacheUpdater, cancellationToken);
    }

    /// <summary>
    /// Ingest a live Twitch alert (EventSub: follow / sub / cheer / raid / gift /
    /// reward redemption) already mapped into a <see cref="TwitchMockPayload"/>.
    /// </summary>
    public Task IngestAlertAsync(
        TwitchMockPayload payload,
        TwitchDisplayCacheUpdater? displayCacheOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        EnsureStarted();
        return PublishMappedEventAsync(
            TwitchEventMapper.Map(payload),
            displayCacheOverride ?? displayCacheUpdater,
            cancellationToken);
    }

    internal Task PublishIrcMessageAsync(TwitchIrcMessage message, CancellationToken cancellationToken = default)
    {
        return PublishIrcMessageAsync(message, displayCacheUpdater, cancellationToken);
    }

    private async Task PublishIrcMessageAsync(
        TwitchIrcMessage message,
        TwitchDisplayCacheUpdater? updater,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureStarted();

        var parsed = TwitchIrcMessageParser.Parse(message);
        if (!TryMarkNew(parsed.Event.Platform, parsed.Event.EventId))
        {
            return;
        }

        if (updater is not null)
        {
            await updater.ApplyChatAsync(parsed.Event, parsed.DisplayHints, cancellationToken);
        }

        await eventBus.PublishAsync(parsed.Event, cancellationToken);
    }

    private async Task PublishMappedEventAsync(
        IStreamEvent streamEvent,
        TwitchDisplayCacheUpdater? updater,
        CancellationToken cancellationToken)
    {
        if (!TryMarkNew(streamEvent.Platform, streamEvent.EventId))
        {
            return;
        }

        if (updater is not null)
        {
            await updater.ApplyAsync(streamEvent, cancellationToken);
        }

        await eventBus.PublishAsync(streamEvent, cancellationToken);
    }

    private bool TryMarkNew(string platform, string sourceEventId)
    {
        return (eventSubDedupCache ??= new EventSubDedupCache(TimeProvider.System)).TryMarkNew(platform, sourceEventId);
    }

    private PkceStateStore GetPkceStateStore()
    {
        return LazyInitializer.EnsureInitialized(
            ref pkceStateStore,
            () => new PkceStateStore(TimeProvider.System));
    }

    private void EnsureStarted()
    {
        if (Volatile.Read(ref _started) is 0)
        {
            throw new InvalidOperationException("TwitchAdapter must be started before publishing events.");
        }
    }
}

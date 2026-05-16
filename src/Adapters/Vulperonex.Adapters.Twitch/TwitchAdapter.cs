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
        return PublishMappedEventAsync(TwitchEventMapper.Map(payload), cancellationToken);
    }

    internal async Task PublishIrcMessageAsync(TwitchIrcMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureStarted();

        var parsed = TwitchIrcMessageParser.Parse(message);
        if (!TryMarkNew(parsed.Event.Platform, parsed.Event.EventId))
        {
            return;
        }

        if (displayCacheUpdater is not null)
        {
            await displayCacheUpdater.ApplyChatAsync(parsed.Event, parsed.DisplayHints, cancellationToken);
        }

        await eventBus.PublishAsync(parsed.Event, cancellationToken);
    }

    private async Task PublishMappedEventAsync(IStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        if (!TryMarkNew(streamEvent.Platform, streamEvent.EventId))
        {
            return;
        }

        if (displayCacheUpdater is not null)
        {
            await displayCacheUpdater.ApplyAsync(streamEvent, cancellationToken);
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

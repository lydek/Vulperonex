using Vulperonex.Adapters.Abstractions;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.EventTypes;
using Vulperonex.Domain.Events;

namespace Vulperonex.Adapters.Twitch;

public sealed class TwitchAdapter(
    IStreamEventBus eventBus,
    IStreamEventTypeRegistry eventTypeRegistry) : IStreamEventSource
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

    public Task PublishMockPayloadAsync(TwitchMockPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return eventBus.PublishAsync(TwitchEventMapper.Map(payload), cancellationToken);
    }
}

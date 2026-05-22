using Vulperonex.Application.EventBus;
using Vulperonex.Application.EventTypes;
using Vulperonex.Domain.Events;

namespace Vulperonex.Adapters.Simulation;

public sealed class SimulationAdapter(
    IStreamEventBus eventBus,
    IStreamEventTypeRegistry eventTypeRegistry) : ISimulationAdapter
{
    private static readonly StreamEventTypeMetadata[] SupportedEventTypes =
    [
        new(StreamEventKeys.UserSentMessage, "Simulated chat message"),
        new(StreamEventKeys.UserFollowed, "Simulated follow"),
        new(StreamEventKeys.UserDonated, "Simulated donation"),
        new(StreamEventKeys.UserSubscribed, "Simulated subscription"),
        new(StreamEventKeys.UserGiftedSubscription, "Simulated gifted subscription"),
        new(StreamEventKeys.ChannelRaided, "Simulated raid"),
        new(StreamEventKeys.RewardRedeemed, "Simulated reward redemption"),
    ];

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var metadata in SupportedEventTypes)
        {
            eventTypeRegistry.Register(metadata);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task<IStreamEvent> SimulateAsync(SimulationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var streamEvent = CreateEvent(request);
        await eventBus.PublishAsync(streamEvent, cancellationToken);
        return streamEvent;
    }

    private static IStreamEvent CreateEvent(SimulationRequest request)
    {
        return request.Kind switch
        {
            SimulationKind.Message => new UserSentMessageEvent
            {
                Platform = request.Platform,
                User = request.User,
                MessageText = request.MessageText ?? string.Empty,
            },
            SimulationKind.Followed => new UserFollowedEvent
            {
                Platform = request.Platform,
                User = request.User,
            },
            SimulationKind.Donated => new UserDonatedEvent
            {
                Platform = request.Platform,
                User = request.User,
                TotalBitsGiven = request.TotalBitsGiven,
            },
            SimulationKind.Subscribed => new UserSubscribedEvent
            {
                Platform = request.Platform,
                User = request.User,
                Tier = request.Tier ?? string.Empty,
            },
            SimulationKind.GiftedSubscription => new UserGiftedSubscriptionEvent
            {
                Platform = request.Platform,
                User = request.User,
                Tier = request.Tier ?? string.Empty,
                GiftCount = request.GiftCount,
            },
            SimulationKind.Raided => new ChannelRaidedEvent
            {
                Platform = request.Platform,
                User = request.User,
                ViewerCount = request.ViewerCount,
            },
            SimulationKind.RewardRedeemed => new RewardRedeemedEvent
            {
                Platform = request.Platform,
                User = request.User,
                RewardId = request.RewardId ?? string.Empty,
                RewardTitle = request.RewardTitle ?? string.Empty,
                RedemptionId = request.RedemptionId ?? string.Empty,
            },
            _ => throw new NotSupportedException($"Unsupported simulation kind: {request.Kind}."),
        };
    }
}

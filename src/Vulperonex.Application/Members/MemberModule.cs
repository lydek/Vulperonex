using Microsoft.Extensions.Hosting;
using Vulperonex.Application.EventBus;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;

namespace Vulperonex.Application.Members;

public sealed class MemberModule(
    IStreamEventBus eventBus,
    IMemberResolver memberResolver,
    IMemberStreamStateRepository streamStateRepository,
    TimeProvider? timeProvider = null) : IHostedService, IDisposable
{
    private readonly MemberEventDedupCache _seenEvents = new(timeProvider ?? TimeProvider.System);
    private readonly List<IDisposable> _subscriptions = [];

    public void Start()
    {
        StartAsync().GetAwaiter().GetResult();
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_subscriptions.Count is 0)
        {
            _subscriptions.Add(eventBus.Subscribe<IStreamEvent>(HandleAsync));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DisposeSubscriptions();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        DisposeSubscriptions();
    }

    private void DisposeSubscriptions()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
    }

    private async Task HandleAsync(IStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        switch (streamEvent)
        {
            case UserFollowedEvent followed:
                var followedIdentity = await ResolveIdentityAsync(followed, cancellationToken);
                if (followedIdentity is not null)
                {
                    await streamStateRepository.MarkFollowerAsync(followedIdentity, cancellationToken);
                }

                break;
            case UserSubscribedEvent subscribed:
                var subscribedIdentity = await ResolveIdentityAsync(subscribed, cancellationToken);
                if (subscribedIdentity is not null)
                {
                    await streamStateRepository.MarkSubscriberAsync(subscribedIdentity, subscribed.Tier, cancellationToken);
                }

                break;
            case { User: not null }:
                await ResolveIdentityAsync(streamEvent, cancellationToken);
                break;
        }
    }

    private async Task<PlatformIdentity?> ResolveIdentityAsync(IStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        if (streamEvent.User is null)
        {
            return null;
        }

        if (!_seenEvents.TryMarkNew(streamEvent.Platform, streamEvent.EventId))
        {
            return null;
        }

        var identity = PlatformIdentity.Create(streamEvent.Platform, streamEvent.User.UserId);
        await memberResolver.ResolveMemberIdAsync(identity, cancellationToken);
        return identity;
    }
}

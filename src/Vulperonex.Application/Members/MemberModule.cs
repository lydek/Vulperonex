using Vulperonex.Application.EventBus;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;

namespace Vulperonex.Application.Members;

public sealed class MemberModule(
    IStreamEventBus eventBus,
    IMemberResolver memberResolver,
    IMemberStreamStateRepository? streamStateRepository = null) : IDisposable
{
    private readonly HashSet<(string Platform, string SourceEventId)> _seenEvents = [];
    private readonly List<IDisposable> _subscriptions = [];

    public void Start()
    {
        _subscriptions.Add(eventBus.Subscribe<UserSentMessageEvent>(ResolveAsync));
        _subscriptions.Add(eventBus.Subscribe<UserFollowedEvent>(HandleFollowedAsync));
        _subscriptions.Add(eventBus.Subscribe<UserSubscribedEvent>(HandleSubscribedAsync));
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
    }

    private Task ResolveAsync(IStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        return ResolveIdentityAsync(streamEvent, cancellationToken);
    }

    private async Task HandleFollowedAsync(UserFollowedEvent streamEvent, CancellationToken cancellationToken)
    {
        var identity = await ResolveIdentityAsync(streamEvent, cancellationToken);
        if (identity is not null && streamStateRepository is not null)
        {
            await streamStateRepository.MarkFollowerAsync(identity, cancellationToken);
        }
    }

    private async Task HandleSubscribedAsync(UserSubscribedEvent streamEvent, CancellationToken cancellationToken)
    {
        var identity = await ResolveIdentityAsync(streamEvent, cancellationToken);
        if (identity is not null && streamStateRepository is not null)
        {
            await streamStateRepository.MarkSubscriberAsync(identity, streamEvent.Tier, cancellationToken);
        }
    }

    private async Task<PlatformIdentity?> ResolveIdentityAsync(IStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        if (streamEvent.User is null)
        {
            return null;
        }

        var dedupKey = (streamEvent.Platform, streamEvent.EventId);
        if (!_seenEvents.Add(dedupKey))
        {
            return null;
        }

        var identity = PlatformIdentity.Create(streamEvent.Platform, streamEvent.User.UserId);
        await memberResolver.ResolveMemberIdAsync(identity, cancellationToken);
        return identity;
    }
}

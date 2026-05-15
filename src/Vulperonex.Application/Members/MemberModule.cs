using Vulperonex.Application.EventBus;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;

namespace Vulperonex.Application.Members;

public sealed class MemberModule(IStreamEventBus eventBus, IMemberResolver memberResolver) : IDisposable
{
    private readonly HashSet<(string Platform, string SourceEventId)> _seenEvents = [];
    private readonly List<IDisposable> _subscriptions = [];

    public void Start()
    {
        _subscriptions.Add(eventBus.Subscribe<UserSentMessageEvent>(ResolveAsync));
        _subscriptions.Add(eventBus.Subscribe<UserFollowedEvent>(ResolveAsync));
        _subscriptions.Add(eventBus.Subscribe<UserSubscribedEvent>(ResolveAsync));
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
        if (streamEvent.User is null)
        {
            return Task.CompletedTask;
        }

        var dedupKey = (streamEvent.Platform, streamEvent.EventId);
        if (!_seenEvents.Add(dedupKey))
        {
            return Task.CompletedTask;
        }

        return memberResolver.ResolveMemberIdAsync(
            PlatformIdentity.Create(streamEvent.Platform, streamEvent.User.UserId),
            cancellationToken);
    }
}

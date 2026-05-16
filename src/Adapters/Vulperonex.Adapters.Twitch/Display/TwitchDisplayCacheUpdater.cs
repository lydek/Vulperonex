using Vulperonex.Adapters.Abstractions;
using Vulperonex.Domain.Events;

namespace Vulperonex.Adapters.Twitch.Display;

public sealed class TwitchDisplayCacheUpdater(IPlatformUserInfoCache cache)
{
    public Task ApplyChatAsync(
        UserSentMessageEvent streamEvent,
        TwitchDisplayHints displayHints,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamEvent);
        ArgumentNullException.ThrowIfNull(displayHints);

        return cache.UpdateAsync(
            streamEvent.Platform,
            streamEvent.User.UserId,
            current => current with
            {
                DisplayName = streamEvent.User.DisplayName,
                AvatarUrl = displayHints.AvatarUrl ?? current.AvatarUrl,
                ColorHex = displayHints.ColorHex ?? current.ColorHex,
                Badges = displayHints.Badges,
                IsSubscriber = displayHints.IsSubscriber || current.IsSubscriber,
                TotalBitsGiven = Math.Max(current.TotalBitsGiven, displayHints.TotalBitsGiven),
            },
            cancellationToken);
    }

    public Task ApplyAsync(IStreamEvent streamEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamEvent);

        return streamEvent switch
        {
            UserSubscribedEvent subscribed => cache.UpdateAsync(
                subscribed.Platform,
                subscribed.User.UserId,
                current => current with
                {
                    DisplayName = subscribed.User.DisplayName,
                    IsSubscriber = true,
                    SubscriptionTier = subscribed.Tier,
                },
                cancellationToken),
            UserDonatedEvent donated => cache.UpdateAsync(
                donated.Platform,
                donated.User.UserId,
                current => current with
                {
                    DisplayName = donated.User.DisplayName,
                    TotalBitsGiven = Math.Max(current.TotalBitsGiven, donated.TotalBitsGiven),
                },
                cancellationToken),
            UserFollowedEvent followed => cache.UpdateAsync(
                followed.Platform,
                followed.User.UserId,
                current => current with
                {
                    DisplayName = followed.User.DisplayName,
                    Badges = current.Badges.Contains("follower", StringComparer.Ordinal)
                        ? current.Badges
                        : current.Badges.Concat(["follower"]).ToArray(),
                },
                cancellationToken),
            _ => Task.CompletedTask,
        };
    }
}

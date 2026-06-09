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
                Login = streamEvent.User.Login ?? current.Login,
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
                    Badges = AddBadge(current.Badges, $"subscriber/{subscribed.Tier}"),
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
                    Badges = AddBadge(current.Badges, "follower"),
                },
                cancellationToken),
            _ => Task.CompletedTask,
        };
    }

    private static IReadOnlyCollection<string> AddBadge(IReadOnlyCollection<string> badges, string badge)
    {
        return badges.Contains(badge, StringComparer.Ordinal)
            ? badges
            : badges.Concat([badge]).ToArray();
    }
}

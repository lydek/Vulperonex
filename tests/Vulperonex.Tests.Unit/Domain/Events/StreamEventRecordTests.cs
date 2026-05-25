using System.Text.RegularExpressions;
using FluentAssertions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Unit.Domain.Events;

public sealed partial class StreamEventRecordTests
{
    [Fact]
    public void Given_MvpEventRecords_When_Constructed_Then_AllEventsExposeCanonicalEventTypeKeys()
    {
        var user = new StreamUser("twitch", "12345", "alice");

        IStreamEvent[] events =
        [
            new UserSentMessageEvent { Platform = "twitch", User = user },
            new UserFollowedEvent { Platform = "twitch", User = user },
            new UserDonatedEvent { Platform = "twitch", User = user },
            new UserSubscribedEvent { Platform = "twitch", User = user },
            new UserGiftedSubscriptionEvent { Platform = "twitch", User = user },
            new ChannelRaidedEvent { Platform = "twitch", User = user },
            new RewardRedeemedEvent { Platform = "twitch", User = user },
            new PlatformConnectionChangedEvent { Platform = "twitch", IsConnected = true },
            new MemberCheckedInEvent { Platform = "twitch", User = user },
        ];

        events.Select(streamEvent => streamEvent.EventTypeKey)
            .Should()
            .Equal(
                StreamEventKeys.UserSentMessage,
                StreamEventKeys.UserFollowed,
                StreamEventKeys.UserDonated,
                StreamEventKeys.UserSubscribed,
                StreamEventKeys.UserGiftedSubscription,
                StreamEventKeys.ChannelRaided,
                StreamEventKeys.RewardRedeemed,
                StreamEventKeys.PlatformConnectionChanged,
                StreamEventKeys.MemberCheckedIn);
    }

    [Fact]
    public void Given_MvpEventRecords_When_ConstructedWithoutEventId_Then_DefaultEventIdsAreUlidStrings()
    {
        var user = new StreamUser("twitch", "12345", "alice");

        IStreamEvent[] events =
        [
            new UserSentMessageEvent { Platform = "twitch", User = user },
            new UserFollowedEvent { Platform = "twitch", User = user },
            new UserDonatedEvent { Platform = "twitch", User = user },
            new UserSubscribedEvent { Platform = "twitch", User = user },
            new UserGiftedSubscriptionEvent { Platform = "twitch", User = user },
            new ChannelRaidedEvent { Platform = "twitch", User = user },
            new RewardRedeemedEvent { Platform = "twitch", User = user },
            new PlatformConnectionChangedEvent { Platform = "twitch", IsConnected = false },
            new MemberCheckedInEvent { Platform = "twitch", User = user },
        ];

        events.Should().OnlyContain(streamEvent => UlidPattern().IsMatch(streamEvent.EventId));
    }

    [Fact]
    public void Given_PlatformConnectionChangedEvent_When_Constructed_Then_UserIsNull()
    {
        var streamEvent = new PlatformConnectionChangedEvent
        {
            Platform = "twitch",
            IsConnected = false,
            Reason = "reconnecting",
        };

        streamEvent.User.Should().BeNull();
        streamEvent.Reason.Should().Be("reconnecting");
    }

    [Fact]
    public void Given_MemberCheckedInEvent_When_Constructed_Then_PropertiesAreCorrect()
    {
        var user = new StreamUser("twitch", "12345", "alice");
        var streamEvent = new MemberCheckedInEvent
        {
            Platform = "twitch",
            User = user,
            AvatarUrl = "http://avatar",
            CheckInCount = 5,
            TotalLoyalty = 100,
            RoundIndex = 1,
            StampSlotInRound = 5,
        };

        streamEvent.AvatarUrl.Should().Be("http://avatar");
        streamEvent.CheckInCount.Should().Be(5);
        streamEvent.TotalLoyalty.Should().Be(100);
        streamEvent.RoundIndex.Should().Be(1);
        streamEvent.StampSlotInRound.Should().Be(5);
    }

    [GeneratedRegex("^[0-9A-HJKMNP-TV-Z]{26}$")]
    private static partial Regex UlidPattern();
}

using FluentAssertions;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Unit.Domain.Events;

public sealed class StreamEventDescriptionsTests
{
    [Fact]
    public void Given_StreamEventDescriptions_When_Read_Then_AllWorkflowVisibleKeysHaveDescriptions()
    {
        var workflowVisibleKeys = new[]
        {
            StreamEventKeys.UserSentMessage,
            StreamEventKeys.UserFollowed,
            StreamEventKeys.UserDonated,
            StreamEventKeys.UserSubscribed,
            StreamEventKeys.UserGiftedSubscription,
            StreamEventKeys.ChannelRaided,
            StreamEventKeys.RewardRedeemed,
            StreamEventKeys.WorkflowTimer,
        };

        foreach (var key in workflowVisibleKeys)
        {
            StreamEventDescriptions.GetDescription(key)
                .Should()
                .NotBeNullOrWhiteSpace($"{key} must have a workflow-visible description");
        }
    }

    [Fact]
    public void Given_StreamEventDescriptions_When_SystemEventKeyIsRead_Then_PlatformConnectionChangedIsSystemOnly()
    {
        StreamEventDescriptions.IsSystemEvent(StreamEventKeys.PlatformConnectionChanged)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Given_StreamEventDescriptions_When_WorkflowVisibleKeysAreRead_Then_SystemEventsAreExcluded()
    {
        StreamEventDescriptions.GetWorkflowVisibleKeys()
            .Should()
            .BeEquivalentTo(
                StreamEventKeys.UserSentMessage,
                StreamEventKeys.UserFollowed,
                StreamEventKeys.UserDonated,
                StreamEventKeys.UserSubscribed,
                StreamEventKeys.UserGiftedSubscription,
                StreamEventKeys.ChannelRaided,
                StreamEventKeys.RewardRedeemed,
                StreamEventKeys.WorkflowTimer);
    }
}

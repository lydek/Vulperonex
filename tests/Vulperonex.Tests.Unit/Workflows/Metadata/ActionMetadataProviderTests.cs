using FluentAssertions;
using System.Reflection;
using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Metadata;
using Vulperonex.Infrastructure.Workflows.Metadata;
using Xunit;

namespace Vulperonex.Tests.Unit.Workflows.Metadata;

public sealed class ActionMetadataProviderTests
{
    [Fact]
    public void Given_ActionMetadataProvider_When_GetAvailableActionsCalled_Then_ReturnsAll15ConcreteActions()
    {
        var provider = new ActionMetadataProvider();
        var actions = provider.GetAvailableActions();

        // 應回傳 15 個 concrete actions
        actions.Should().HaveCount(15);

        // 驗證幾款典型的 Action
        var sendChat = actions.Should().ContainSingle(a => a.Type == "sendChatMessage").Subject;
        sendChat.DisplayName.Should().Be("Send Chat Message");
        sendChat.Description.Should().Be("Send a message to a stream platform chat");
        sendChat.Parameters.Should().Contain(p => p.Key == "Template" && p.Type == "string" && p.Required);
        // TargetPlatform, Channel & DedupKey are intentionally not exposed as editor parameters (internal / automatic).
        sendChat.Parameters.Should().NotContain(p => p.Key == "TargetPlatform");
        sendChat.Parameters.Should().NotContain(p => p.Key == "Channel");
        sendChat.Parameters.Should().NotContain(p => p.Key == "DedupKey");

        var delay = actions.Should().ContainSingle(a => a.Type == "delay").Subject;
        delay.DisplayName.Should().Be("Delay");
        delay.Parameters.Should().Contain(p => p.Key == "DelayMs" && p.Type == "number");

        var addLottery = actions.Should().ContainSingle(a => a.Type == "addLotteryTickets").Subject;
        addLottery.DisplayName.Should().Be("Add Lottery Tickets");
        addLottery.Parameters.Should().Contain(p => p.Key == "UserId" && p.Type == "string");
        addLottery.Parameters.Should().Contain(p => p.Key == "Amount" && p.Type == "number");
    }

    [Fact]
    public void Given_AllWorkflowActionDerivedTypes_When_Instantiated_Then_AllHaveActionMetadataAttribute()
    {
        // 反射掃描所有非抽象且繼承自 WorkflowAction 的 type
        var actionTypes = typeof(WorkflowAction).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(WorkflowAction)));

        foreach (var type in actionTypes)
        {
            var metadataAttr = type.GetCustomAttribute<ActionMetadataAttribute>();
            metadataAttr.Should().NotBeNull($"Action class {type.Name} must be annotated with [ActionMetadata]");
        }
    }
}

using System.Text.Json;
using FluentAssertions;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Domain;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows;

public sealed class WorkflowContractTests
{
    [Fact]
    public void Given_WorkflowPorts_When_Inspected_Then_WriteAndReadContractsAreSeparate()
    {
        typeof(IWorkflowRuleRepository).GetMethods().Select(method => method.Name)
            .Should().BeEquivalentTo("AddAsync", "UpdateAsync", "DeleteAsync", "SetEnabledAsync");

        typeof(IWorkflowRuleQueryService).GetMethods().Select(method => method.Name)
            .Should().BeEquivalentTo("ListEnabledByEventTypeAsync", "ListAsync", "GetAsync");
    }

    [Fact]
    public void Given_WorkflowRuleContract_When_Created_Then_ItDoesNotExposeInfrastructureEntityTypes()
    {
        var rule = new WorkflowRule
        {
            Id = "rule-1",
            Name = "Chat hello",
            EventTypeKey = "user.message",
            Conditions =
            [
                new MessageContentCondition
                {
                    MatchMode = MessageContentMatchMode.PrefixMatch,
                    Pattern = "!hello",
                },
            ],
            Actions =
            [
                new SendChatMessageAction
                {
                    Template = "Hello {user.displayName}",
                },
            ],
        };

        rule.Conditions.Should().ContainSingle()
            .Which.Type.Should().Be(MessageContentCondition.ConditionType);
        rule.Actions.Should().ContainSingle()
            .Which.Type.Should().Be(SendChatMessageAction.ActionType);
    }

    [Fact]
    public void Given_ActionAndConditionDtos_When_Serialized_Then_TheyRoundTripWithExplicitTypeDiscriminators()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        WorkflowCondition condition = new UserRoleCondition
        {
            Roles = StreamRole.Subscriber | StreamRole.Vip,
            Mode = UserRoleMatchMode.HasAny,
        };
        WorkflowAction action = new InvokeSubWorkflowAction
        {
            WorkflowId = "workflow-2",
            TimeoutMs = 1_000,
            MaxRetries = 2,
            BackoffMs = 250,
            ErrorBehavior = ErrorBehavior.RetryOnError,
        };

        var conditionJson = JsonSerializer.Serialize(condition, options);
        var actionJson = JsonSerializer.Serialize(action, options);

        conditionJson.Should().Contain("\"type\":\"userRole\"");
        actionJson.Should().Contain("\"type\":\"invokeSubWorkflow\"");
        JsonSerializer.Deserialize<WorkflowCondition>(conditionJson, options)
            .Should().BeEquivalentTo(condition);
        JsonSerializer.Deserialize<WorkflowAction>(actionJson, options)
            .Should().BeEquivalentTo(action);
    }
}

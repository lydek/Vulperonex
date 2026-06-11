using FluentAssertions;
using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Workflows.Metadata;
using Vulperonex.Infrastructure.EventTypes;
using Vulperonex.Infrastructure.Workflows.Metadata;
using Xunit;

namespace Vulperonex.Tests.Unit.Workflows.Metadata;

public sealed class TriggerMetadataProviderTests
{
    [Fact]
    public void Given_KnownEventTypes_When_GetAvailableEventTypesCalled_Then_ReturnsMappedDisplayNames()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        registry.Register(new StreamEventTypeMetadata("user.message", "User sent message", IsSystemEvent: false));
        registry.Register(new StreamEventTypeMetadata("workflow.timer", "Timer fired", IsSystemEvent: false));
        
        var provider = new TriggerMetadataProvider(registry);
        var types = provider.GetAvailableEventTypes();

        types.Should().HaveCount(2);
        var messageType = types.Should().ContainSingle(t => t.Key == "user.message").Subject;
        messageType.DisplayName.Should().Be("User Message");
        messageType.Description.Should().Be("User sent message");

        var timerType = types.Should().ContainSingle(t => t.Key == "workflow.timer").Subject;
        timerType.DisplayName.Should().Be("Workflow Timer");
    }

    [Fact]
    public void Given_UserMessageEventKey_When_GetFilterFieldsCalled_Then_ReturnsCommandNameAndPrefix()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        var provider = new TriggerMetadataProvider(registry);

        var fields = provider.GetFilterFieldsFor("user.message");

        fields.Should().HaveCount(2);
        fields.Should().Contain(f => f.Key == "CommandName" && f.Type == "string");
        fields.Should().Contain(f => f.Key == "Prefix" && f.Type == "string");
    }

    [Fact]
    public void Given_UserMessageEventKey_When_GetValidVariablesCalled_Then_ReturnsCommonAndCustomVariables()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        var provider = new TriggerMetadataProvider(registry);

        var variables = provider.GetValidVariablesFor("user.message");

        variables.Should().Contain("EventId");
        variables.Should().Contain("EventTypeKey");
        variables.Should().Contain("Platform");
        variables.Should().Contain("OccurredAt");
        variables.Should().Contain("MessageText");
        variables.Should().NotContain("UserId");
        variables.Should().NotContain("Channel");
    }

    [Fact]
    public void Given_WorkflowTimerEventKey_When_GetMetadataCalled_Then_UsesTimerPayloadVariables()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        var provider = new TriggerMetadataProvider(registry);

        var fields = provider.GetFilterFieldsFor("workflow.timer");
        var variables = provider.GetValidVariablesFor("workflow.timer");

        fields.Should().ContainSingle(field => field.Key == "TimerId");
        variables.Should().Contain("Payload.TimerId");
        variables.Should().Contain("Payload.RuleId");
        variables.Should().Contain("Payload.IntervalSeconds");
        variables.Should().NotContain("TimerName");
    }

    [Fact]
    public void Given_CustomPluginEventKey_When_GetValidVariablesCalled_Then_ReturnsPluginPayloadVariables()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        var provider = new TriggerMetadataProvider(registry);

        var variables = provider.GetValidVariablesFor("custom.plugin");

        variables.Should().Contain("Payload.PluginId");
        variables.Should().Contain("Payload.PluginName");
        variables.Should().Contain("Payload.ActionId");
        variables.Should().Contain("Payload.ActionName");
        variables.Should().Contain("Payload.ModuleName");
    }
}

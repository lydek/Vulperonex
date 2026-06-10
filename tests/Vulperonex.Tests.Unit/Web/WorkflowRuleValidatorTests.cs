using System.Text.Json;
using FluentAssertions;
using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Metadata;
using Vulperonex.Web.Validation;
using Vulperonex.Web.Workflows;
using Xunit;

namespace Vulperonex.Tests.Unit.Web;

public sealed class WorkflowRuleValidatorTests
{
    [Fact]
    public void Given_LookupPlatformUserActionWithTarget_When_Validated_Then_TargetSatisfiesRequiredParam()
    {
        var validator = new WorkflowRuleValidator(
            new KnownEventTypeRegistry(),
            new EmptyTriggerMetadataProvider());
        var action = JsonSerializer.SerializeToElement(new
        {
            type = LookupPlatformUserAction.ActionType,
            target = "{Member.DisplayName}",
        });
        var request = new WorkflowRuleUpsertRequest(
            Id: null,
            Name: "Lookup user",
            EventTypeKey: "user.message",
            IsEnabled: true,
            Priority: 0,
            Conditions: [],
            Actions: [action]);

        var error = validator.Validate(request);

        error.Should().BeNull();
    }

    private sealed class KnownEventTypeRegistry : IStreamEventTypeRegistry
    {
        public void Register(StreamEventTypeMetadata metadata)
        {
        }

        public bool IsKnown(string key) => true;

        public bool IsKnownForWorkflow(string key) => true;

        public IReadOnlyCollection<StreamEventTypeMetadata> GetAll() => [];
    }

    private sealed class EmptyTriggerMetadataProvider : ITriggerMetadataProvider
    {
        public IReadOnlyList<EventTypeMetadataDto> GetAvailableEventTypes() => [];

        public IReadOnlyList<FilterFieldDto> GetFilterFieldsFor(string eventTypeKey) => [];

        public IReadOnlyList<string> GetValidVariablesFor(string eventTypeKey) => [];
    }
}

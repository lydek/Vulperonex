using System.Text.Json.Serialization;

namespace Vulperonex.Application.Workflows.Conditions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UserRoleCondition), UserRoleCondition.ConditionType)]
[JsonDerivedType(typeof(MessageContentCondition), MessageContentCondition.ConditionType)]
[JsonDerivedType(typeof(CooldownCondition), CooldownCondition.ConditionType)]
public abstract record WorkflowCondition
{
    [JsonIgnore]
    public abstract string Type { get; }
}

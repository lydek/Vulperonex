using Vulperonex.Domain;
using System.Text.Json.Serialization;

namespace Vulperonex.Application.Workflows.Conditions;

public sealed record UserRoleCondition : WorkflowCondition
{
    public const string ConditionType = "userRole";

    [JsonIgnore]
    public override string Type => ConditionType;
    public StreamRole Roles { get; init; }
    public UserRoleMatchMode Mode { get; init; } = UserRoleMatchMode.HasAny;
}

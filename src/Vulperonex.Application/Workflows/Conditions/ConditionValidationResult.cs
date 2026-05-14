namespace Vulperonex.Application.Workflows.Conditions;

public sealed record ConditionValidationResult(bool IsValid, string? ErrorCode = null)
{
    public static readonly ConditionValidationResult Valid = new(true);
}

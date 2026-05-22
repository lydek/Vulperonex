namespace Vulperonex.Application.Workflows;

public sealed record WorkflowThrottlePolicy(
    int MaxConcurrent = 0,
    int CooldownSeconds = 0,
    bool PerUserCooldown = false,
    int PerUserCooldownSeconds = 0)
{
    public static WorkflowThrottlePolicy None { get; } = new();
}

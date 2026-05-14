namespace Vulperonex.Infrastructure.EventBus;

public static class ActionExecutionKey
{
    public static string Compose(
        string eventId,
        string workflowRuleId,
        int actionIndex,
        string? invocationId = null)
    {
        var baseKey = $"{eventId}:{workflowRuleId}:{actionIndex}";
        return string.IsNullOrWhiteSpace(invocationId)
            ? baseKey
            : $"{baseKey}:{invocationId}";
    }
}

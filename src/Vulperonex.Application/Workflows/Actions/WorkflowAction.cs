using System.Text.Json.Serialization;

namespace Vulperonex.Application.Workflows.Actions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SendChatMessageAction), SendChatMessageAction.ActionType)]
[JsonDerivedType(typeof(InvokeSubWorkflowAction), InvokeSubWorkflowAction.ActionType)]
[JsonDerivedType(typeof(InvokePluginAction), InvokePluginAction.ActionType)]
[JsonDerivedType(typeof(DelayAction), DelayAction.ActionType)]
[JsonDerivedType(typeof(StopIfAction), StopIfAction.ActionType)]
[JsonDerivedType(typeof(RandomPickerAction), RandomPickerAction.ActionType)]
[JsonDerivedType(typeof(UpdateCounterAction), UpdateCounterAction.ActionType)]
[JsonDerivedType(typeof(TriggerCheckInAction), TriggerCheckInAction.ActionType)]
[JsonDerivedType(typeof(AddLotteryTicketsAction), AddLotteryTicketsAction.ActionType)]
[JsonDerivedType(typeof(EmitSystemEventAction), EmitSystemEventAction.ActionType)]
[JsonDerivedType(typeof(TriggerEffectAction), TriggerEffectAction.ActionType)]
[JsonDerivedType(typeof(EmitOverlayWidgetAction), EmitOverlayWidgetAction.ActionType)]
[JsonDerivedType(typeof(LookupTwitchUserAction), LookupTwitchUserAction.ActionType)]
[JsonDerivedType(typeof(ShoutoutAction), ShoutoutAction.ActionType)]
[JsonDerivedType(typeof(RefundTwitchRedemptionAction), RefundTwitchRedemptionAction.ActionType)]
public abstract record WorkflowAction
{
    [JsonIgnore]
    public abstract string Type { get; }
    public string? ExecutionCondition { get; init; }
    public string? OutputVariable { get; init; }
    public int TimeoutMs { get; init; } = 10_000;
    public int MaxRetries { get; init; }
    public int BackoffMs { get; init; } = 500;
    public ErrorBehavior ErrorBehavior { get; init; } = ErrorBehavior.StopOnError;
}

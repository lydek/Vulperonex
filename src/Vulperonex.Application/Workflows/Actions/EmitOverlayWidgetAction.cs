namespace Vulperonex.Application.Workflows.Actions;

using System.Text.Json.Serialization;

public sealed record EmitOverlayWidgetAction : WorkflowAction
{
    public const string ActionType = "emitOverlayWidget";

    [JsonIgnore]
    public override string Type => ActionType;

    public required string WidgetType { get; init; }

    public string OverlayTarget { get; init; } = "alerts";

    public required string DisplayText { get; init; }

    public string Severity { get; init; } = "info";

    public int? DurationMs { get; init; }
}

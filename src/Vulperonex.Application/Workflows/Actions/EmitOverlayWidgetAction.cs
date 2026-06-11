using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Emit Overlay Widget", "Display a widget on the stream overlay")]
public sealed record EmitOverlayWidgetAction : WorkflowAction
{
    public const string ActionType = "emitOverlayWidget";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Widget Type", "string", required: true, help: "Type of the widget to emit, e.g. chat, alert")]
    public required string WidgetType { get; init; }

    [ActionParam("Overlay Target", "string", required: false, help: "Which overlay scene shows the widget", Options = new[] { "alerts", "chat", "member" })]
    public string OverlayTarget { get; init; } = "alerts";

    [ActionParam("Display Text", "string", required: true, help: "Text pattern to display on overlay")]
    public required string DisplayText { get; init; }

    [ActionParam("Severity", "string", required: false, help: "Alert severity level", Options = new[] { "info", "warning", "error" })]
    public string Severity { get; init; } = "info";

    [ActionParam("Duration (ms)", "number", required: false, help: "Custom display duration in milliseconds")]
    public int? DurationMs { get; init; }
}

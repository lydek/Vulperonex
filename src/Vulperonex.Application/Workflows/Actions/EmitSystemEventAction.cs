namespace Vulperonex.Application.Workflows.Actions;

using System.Text.Json.Serialization;

public sealed record EmitSystemEventAction : WorkflowAction
{
    public const string ActionType = "emitSystemEvent";

    [JsonIgnore]
    public override string Type => ActionType;

    public required string EventTypeKey { get; init; }

    public IReadOnlyDictionary<string, string> Payload { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

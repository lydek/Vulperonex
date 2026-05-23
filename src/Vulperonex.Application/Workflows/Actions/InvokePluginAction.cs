using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vulperonex.Application.Workflows.Actions;

public sealed record InvokePluginAction : WorkflowAction
{
    public const string ActionType = "invokePlugin";

    [JsonIgnore]
    public override string Type => ActionType;
    public required string PluginId { get; init; }
    public required string ActionId { get; init; }
    public IReadOnlyDictionary<string, JsonElement> Params { get; init; } =
        new Dictionary<string, JsonElement>();
    public IReadOnlyDictionary<string, string> Args { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

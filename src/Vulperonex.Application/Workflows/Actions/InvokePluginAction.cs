using System.Text.Json;
using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Invoke Plugin", "Invoke a registered plugin action")]
public sealed record InvokePluginAction : WorkflowAction
{
    public const string ActionType = "invokePlugin";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Plugin ID", "string", required: true, help: "Registered unique plugin identifier")]
    public required string PluginId { get; init; }

    [ActionParam("Action ID", "string", required: true, help: "Unique action identifier within the plugin")]
    public required string ActionId { get; init; }

    [ActionParam("Params", "dictionary", required: false, help: "Dynamic JSON-compatible structured parameters")]
    public IReadOnlyDictionary<string, JsonElement> Params { get; init; } =
        new Dictionary<string, JsonElement>();

    [ActionParam("Args", "dictionary", required: false, help: "String dictionary parameters for execution")]
    public IReadOnlyDictionary<string, string> Args { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

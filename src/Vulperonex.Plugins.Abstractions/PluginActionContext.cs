using System.Text.Json;
using Vulperonex.Application.Workflows;
using Vulperonex.Domain.Events;

namespace Vulperonex.Plugins.Abstractions;

public sealed record PluginActionContext(
    ActionExecutionKey ActionExecutionKey,
    IStreamEvent StreamEvent,
    string WorkflowRuleId,
    int ActionIndex,
    string EventTypeKey,
    IReadOnlyDictionary<string, JsonElement> Params) : IPluginActionContext;

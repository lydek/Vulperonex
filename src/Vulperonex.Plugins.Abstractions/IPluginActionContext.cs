using System.Text.Json;
using Vulperonex.Application.Workflows;
using Vulperonex.Domain.Events;

namespace Vulperonex.Plugins.Abstractions;

public interface IPluginActionContext
{
    ActionExecutionKey ActionExecutionKey { get; }

    IStreamEvent StreamEvent { get; }

    string WorkflowRuleId { get; }

    int ActionIndex { get; }

    string EventTypeKey { get; }

    IReadOnlyDictionary<string, JsonElement> Params { get; }
}

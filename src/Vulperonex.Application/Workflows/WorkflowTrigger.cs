using System.Text.Json.Serialization;

namespace Vulperonex.Application.Workflows;

public sealed record WorkflowTrigger
{
    public IReadOnlyDictionary<string, string> Filter { get; init; }

    public WorkflowTrigger(IReadOnlyDictionary<string, string>? filter = null)
    {
        Filter = filter ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

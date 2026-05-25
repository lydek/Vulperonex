namespace Vulperonex.Application.Modules;

public sealed record ModuleDefinition(
    string Name,
    string DisplayName,
    string Kind,
    IReadOnlyList<string> Dependencies,
    bool EnabledByDefault = true);

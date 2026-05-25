namespace Vulperonex.Application.Modules;

public sealed record ModuleStateSnapshot(
    string Name,
    string DisplayName,
    string Kind,
    bool Enabled,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Dependents);

public sealed record ModuleToggleResult(
    ModuleStateSnapshot Module,
    IReadOnlyList<ModuleStateSnapshot> ChangedModules);

namespace Vulperonex.Application.Modules;

public interface IModuleStateService
{
    Task<IReadOnlyList<ModuleStateSnapshot>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> IsEnabledAsync(string moduleName, CancellationToken cancellationToken = default);

    Task<ModuleToggleResult> ToggleAsync(
        string moduleName,
        bool enabled,
        string actorKind,
        CancellationToken cancellationToken = default);
}

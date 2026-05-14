namespace Vulperonex.Plugins.Abstractions;

public interface IVulperonexPlugin
{
    string Name { get; }

    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);

    Task ExecuteActionAsync(
        string actionId,
        IPluginActionContext context,
        CancellationToken cancellationToken = default);
}

using Vulperonex.Plugins.Abstractions;

namespace Vulperonex.Plugins.OneCommeBridge;

public sealed class OneCommeBridgePlugin : IVulperonexPlugin
{
    public string Name => "OneCommeBridge";

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task ExecuteActionAsync(
        string actionId,
        IPluginActionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("OneComme bridge scaffold does not expose actions yet.");
    }
}

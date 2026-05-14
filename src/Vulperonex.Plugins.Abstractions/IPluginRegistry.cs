namespace Vulperonex.Plugins.Abstractions;

public interface IPluginRegistry
{
    IVulperonexPlugin? Find(string pluginId);
}

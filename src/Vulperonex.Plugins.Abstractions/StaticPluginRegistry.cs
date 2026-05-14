namespace Vulperonex.Plugins.Abstractions;

public sealed class StaticPluginRegistry(IEnumerable<IVulperonexPlugin> plugins) : IPluginRegistry
{
    private readonly IReadOnlyDictionary<string, IVulperonexPlugin> _pluginsById = plugins
        .GroupBy(plugin => plugin.Name, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    public IVulperonexPlugin? Find(string pluginId)
    {
        return _pluginsById.GetValueOrDefault(pluginId);
    }
}

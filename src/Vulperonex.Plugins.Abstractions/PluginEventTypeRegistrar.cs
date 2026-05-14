using Vulperonex.Application.EventTypes;

namespace Vulperonex.Plugins.Abstractions;

public sealed class PluginEventTypeRegistrar(IStreamEventTypeRegistry registry) : IPluginEventTypeRegistrar
{
    public void Register(PluginEventTypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        registry.Register(new StreamEventTypeMetadata(metadata.Key, metadata.Description, IsSystemEvent: false));
    }
}

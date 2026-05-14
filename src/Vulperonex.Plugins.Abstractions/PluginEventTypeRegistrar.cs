using Vulperonex.Application.EventTypes;

namespace Vulperonex.Plugins.Abstractions;

public sealed class PluginEventTypeRegistrar(IStreamEventTypeRegistry registry) : IPluginEventTypeRegistrar
{
    public void Register(StreamEventTypeMetadata metadata)
    {
        registry.Register(metadata with { IsSystemEvent = false });
    }
}

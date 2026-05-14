using Vulperonex.Application.EventTypes;

namespace Vulperonex.Plugins.Abstractions;

public interface IPluginEventTypeRegistrar
{
    void Register(StreamEventTypeMetadata metadata);
}

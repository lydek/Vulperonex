using Vulperonex.Application.EventBus;

namespace Vulperonex.Plugins.Abstractions;

public interface IPluginContext
{
    IStreamEventBus Events { get; }

    IPluginEventTypeRegistrar EventTypes { get; }
}

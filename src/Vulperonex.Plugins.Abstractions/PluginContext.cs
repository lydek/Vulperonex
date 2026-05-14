using Vulperonex.Application.EventBus;

namespace Vulperonex.Plugins.Abstractions;

public sealed record PluginContext(IStreamEventBus Events, IPluginEventTypeRegistrar EventTypes) : IPluginContext;

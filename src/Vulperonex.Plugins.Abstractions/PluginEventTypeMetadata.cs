namespace Vulperonex.Plugins.Abstractions;

/// <summary>
/// Narrow registration surface for plugin-authored event types.
/// Plugins describe a workflow-visible event key; system-event classification
/// is reserved for first-party adapters and not exposed here.
/// </summary>
public sealed record PluginEventTypeMetadata(string Key, string Description);

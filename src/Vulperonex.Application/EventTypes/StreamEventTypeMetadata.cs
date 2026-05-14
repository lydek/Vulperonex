namespace Vulperonex.Application.EventTypes;

public sealed record StreamEventTypeMetadata(string Key, string Description, bool IsSystemEvent = false);

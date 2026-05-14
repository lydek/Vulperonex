namespace Vulperonex.Application.Settings;

public sealed record SettingChangedEvent(string Key, string? OldValue, string NewValue);

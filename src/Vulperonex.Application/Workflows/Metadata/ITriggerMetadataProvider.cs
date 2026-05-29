namespace Vulperonex.Application.Workflows.Metadata;

public interface ITriggerMetadataProvider
{
    IReadOnlyList<EventTypeMetadataDto> GetAvailableEventTypes();
    IReadOnlyList<FilterFieldDto> GetFilterFieldsFor(string eventTypeKey);
    IReadOnlyList<string> GetValidVariablesFor(string eventTypeKey);
}

public sealed record EventTypeMetadataDto(string Key, string DisplayName, string Description);

public sealed record FilterFieldDto(
    string Key,
    string Label,
    string Type,
    IReadOnlyList<string>? Options = null,
    string? Help = null,
    bool Required = false,
    IReadOnlyList<string>? OptionLabels = null);

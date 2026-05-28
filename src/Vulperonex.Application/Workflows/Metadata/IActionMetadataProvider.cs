namespace Vulperonex.Application.Workflows.Metadata;

public interface IActionMetadataProvider
{
    IReadOnlyList<ActionMetadataDto> GetAvailableActions();
}

public sealed record ActionMetadataDto(
    string Type,
    string DisplayName,
    string Description,
    IReadOnlyList<ActionParamMetadataDto> Parameters);

public sealed record ActionParamMetadataDto(
    string Key,
    string Label,
    string Type,
    bool Required,
    string? Help);

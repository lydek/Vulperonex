namespace Vulperonex.Application.Overlay.Extensions;

public interface IOverlayTemplateImporter
{
    Task<OverlayTemplateImportResult> ImportAsync(
        Stream package,
        string targetSlug,
        CancellationToken cancellationToken = default);
}

public sealed record OverlayTemplateImportResult(
    bool Success,
    string TargetSlug,
    IReadOnlyCollection<string> Warnings);

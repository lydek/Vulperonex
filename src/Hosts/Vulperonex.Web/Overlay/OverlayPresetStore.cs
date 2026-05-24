using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Vulperonex.Web.Overlay;

public sealed class OverlayPresetStore(IWebHostEnvironment environment, ILogger<OverlayPresetStore> logger)
{
    public const long MaxUploadBytes = 5 * 1024 * 1024;
    private static readonly Regex SlugPattern = new("^[a-z0-9-]{1,64}$", RegexOptions.Compiled);

    private static readonly IReadOnlyList<OverlayPresetDescriptor> BuiltInPresets =
    [
        new("chat", "vulperonex-default", "builtin", "Vulperonex default", "/overlay/chat"),
        new("chat", "compact-line", "builtin", "Compact line", "/overlay/chat"),
        new("chat", "member-card-inline", "builtin", "Member card inline", "/overlay/chat"),
        new("member", "rotan-checkin", "builtin", "Rotan checkin", "/overlay/member"),
        new("alerts", "vulperonex-alerts", "builtin", "Vulperonex alerts", "/overlay/alerts"),
    ];

    public IReadOnlyList<OverlayPresetDescriptor> GetBuiltIns() => BuiltInPresets;

    public string GetSettingKeyForHub(string hubName) => hubName switch
    {
        "chat" => Vulperonex.Application.Settings.SystemSettingKey.OverlayChatPreset,
        "member" => Vulperonex.Application.Settings.SystemSettingKey.OverlayMemberPreset,
        "alerts" => Vulperonex.Application.Settings.SystemSettingKey.OverlayAlertsPreset,
        _ => throw new ArgumentOutOfRangeException(nameof(hubName)),
    };

    public bool IsSupportedHub(string hubName) =>
        string.Equals(hubName, "chat", StringComparison.Ordinal)
        || string.Equals(hubName, "member", StringComparison.Ordinal)
        || string.Equals(hubName, "alerts", StringComparison.Ordinal);

    public bool IsValidSlug(string slug) => SlugPattern.IsMatch(slug);

    public string? ResolveCustomRelativePath(string slug)
    {
        if (!IsValidSlug(slug))
        {
            return null;
        }

        var directory = Path.Combine(GetCustomRootPath(), slug);
        var indexPath = Path.Combine(directory, "index.html");
        return File.Exists(indexPath) ? $"/overlay/custom/{slug}/index.html" : null;
    }

    public async Task<OverlayCustomPresetMetadata> SaveAsync(string slug, IFormFile file, CancellationToken cancellationToken)
    {
        if (!IsValidSlug(slug))
        {
            throw new ArgumentException("Invalid slug.", nameof(slug));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".html" and not ".zip")
        {
            throw new InvalidDataException("Unsupported preset package format.");
        }

        var customRoot = GetCustomRootPath();
        Directory.CreateDirectory(customRoot);
        var tempRoot = Path.Combine(customRoot, $".upload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            if (extension == ".html")
            {
                await SaveHtmlAsync(tempRoot, file, cancellationToken);
            }
            else
            {
                await SaveZipAsync(tempRoot, file, cancellationToken);
            }

            var indexPath = Path.Combine(tempRoot, "index.html");
            if (!File.Exists(indexPath))
            {
                throw new InvalidDataException("Custom preset package must contain index.html at archive root.");
            }

            var destination = Path.Combine(customRoot, slug);
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }

            Directory.Move(tempRoot, destination);
            return BuildMetadata(slug, destination);
        }
        catch
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }

            throw;
        }
    }

    public IReadOnlyList<OverlayCustomPresetMetadata> ListCustom()
    {
        var customRoot = GetCustomRootPath();
        if (!Directory.Exists(customRoot))
        {
            return [];
        }

        return Directory.GetDirectories(customRoot)
            .Select(path => BuildMetadata(Path.GetFileName(path), path))
            .OrderBy(entry => entry.Slug, StringComparer.Ordinal)
            .ToArray();
    }

    public bool Delete(string slug)
    {
        if (!IsValidSlug(slug))
        {
            return false;
        }

        var directory = Path.Combine(GetCustomRootPath(), slug);
        if (!Directory.Exists(directory))
        {
            return false;
        }

        Directory.Delete(directory, recursive: true);
        return true;
    }

    public IReadOnlyList<OverlayPresetDescriptor> ListAll()
    {
        var custom = ListCustom()
            .SelectMany(entry => new[]
            {
                new OverlayPresetDescriptor("chat", $"custom:{entry.Slug}", "custom", entry.Slug, $"/overlay/custom/{entry.Slug}/index.html"),
                new OverlayPresetDescriptor("member", $"custom:{entry.Slug}", "custom", entry.Slug, $"/overlay/custom/{entry.Slug}/index.html"),
                new OverlayPresetDescriptor("alerts", $"custom:{entry.Slug}", "custom", entry.Slug, $"/overlay/custom/{entry.Slug}/index.html"),
            })
            .ToArray();

        return [.. BuiltInPresets, .. custom];
    }

    public string GetCustomRootPath() => Path.Combine(environment.WebRootPath, "overlay", "custom");

    private static OverlayCustomPresetMetadata BuildMetadata(string slug, string directory)
    {
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            : [];
        var sizeBytes = files.Sum(path => new FileInfo(path).Length);
        var uploadedAt = files.Length == 0
            ? DateTimeOffset.UtcNow
            : new DateTimeOffset(files.Max(path => File.GetLastWriteTimeUtc(path)));

        return new OverlayCustomPresetMetadata(slug, sizeBytes, uploadedAt);
    }

    private static async Task SaveHtmlAsync(string tempRoot, IFormFile file, CancellationToken cancellationToken)
    {
        var targetPath = Path.Combine(tempRoot, "index.html");
        await using var target = File.Create(targetPath);
        await using var source = file.OpenReadStream();
        await source.CopyToAsync(target, cancellationToken);
    }

    private async Task SaveZipAsync(string tempRoot, IFormFile file, CancellationToken cancellationToken)
    {
        var rootPath = Path.GetFullPath(tempRoot);
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        await using var source = file.OpenReadStream();
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: false);

        long extractedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            extractedBytes += entry.Length;
            if (extractedBytes > MaxUploadBytes)
            {
                throw new InvalidDataException("Expanded preset package exceeds the upload limit.");
            }

            var destinationPath = Path.GetFullPath(Path.Combine(tempRoot, entry.FullName));
            if (!destinationPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            {
                logger.LogWarning("Rejected custom preset zip entry outside root: {EntryName}", entry.FullName);
                throw new InvalidDataException("Zip entry escapes the destination root.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await using var entryStream = entry.Open();
            await using var destination = File.Create(destinationPath);
            await entryStream.CopyToAsync(destination, cancellationToken);
        }
    }
}

public sealed record OverlayCustomPresetMetadata(
    string Slug,
    long SizeBytes,
    DateTimeOffset UploadedAt);

public sealed record OverlayPresetDescriptor(
    string HubName,
    string Key,
    string Kind,
    string Label,
    string RelativeUrl);

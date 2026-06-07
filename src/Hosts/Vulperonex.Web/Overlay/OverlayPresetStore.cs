using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Vulperonex.Web.Overlay;

public sealed class OverlayPresetStore
{
    public const long MaxAssetBytes = 2 * 1024 * 1024;
    private static readonly string[] AllowedImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".gif"];

    private readonly IWebHostEnvironment environment;
    private readonly ILogger<OverlayPresetStore> logger;

    public OverlayPresetStore(IWebHostEnvironment environment, ILogger<OverlayPresetStore> logger)
    {
        this.environment = environment;
        this.logger = logger;
    }

    private static readonly IReadOnlyList<OverlayPresetDescriptor> BuiltInPresets =
    [
        new("chat", "vulperonex-default", "builtin", "Vulperonex default", "/overlay/chat.html"),
        new("chat", "compact-line", "builtin", "Compact line", "/overlay/chat.html"),
        new("chat", "member-card-inline", "builtin", "Member card inline", "/overlay/chat.html"),
        new("member", "rotan-checkin", "builtin", "Rotan checkin", "/overlay/member-card.html"),
        new("alerts", "vulperonex-alerts", "builtin", "Vulperonex alerts", "/overlay/alerts"),
    ];

    public IReadOnlyList<OverlayPresetDescriptor> GetBuiltIns() => BuiltInPresets;

    public IReadOnlyList<OverlayPresetDescriptor> ListAll() => BuiltInPresets;

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

    /// <summary>
    /// Persists an uploaded overlay customization image (background / stamp) under
    /// <c>wwwroot/overlay/assets</c> and returns its served relative URL. Images only;
    /// extension and size are validated by the caller and here.
    /// </summary>
    public async Task<string> SaveAssetAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
        {
            throw new InvalidDataException("Unsupported image format.");
        }

        if (file.Length > MaxAssetBytes)
        {
            throw new InvalidDataException("Image exceeds the size limit.");
        }

        var contentType = file.ContentType ?? string.Empty;
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Uploaded file is not an image.");
        }

        await using (var probe = file.OpenReadStream())
        {
            var header = new byte[12];
            var read = await probe.ReadAsync(header.AsMemory(0, header.Length), cancellationToken).ConfigureAwait(false);
            if (!HasAllowedImageSignature(header, read, extension))
            {
                throw new InvalidDataException("Uploaded file does not match an allowed image format.");
            }
        }

        var assetsDir = Path.Combine(environment.WebRootPath, "overlay", "assets");
        Directory.CreateDirectory(assetsDir);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(assetsDir, fileName);

        await using (var target = File.Create(fullPath))
        await using (var source = file.OpenReadStream())
        {
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Saved overlay customization asset: {FileName} ({Bytes} bytes)", fileName, file.Length);
        return $"/overlay/assets/{fileName}";
    }

    private static bool HasAllowedImageSignature(byte[] header, int length, string extension) => extension switch
    {
        ".png" => length >= 8
            && header[0] == 0x89
            && header[1] == 0x50
            && header[2] == 0x4E
            && header[3] == 0x47
            && header[4] == 0x0D
            && header[5] == 0x0A
            && header[6] == 0x1A
            && header[7] == 0x0A,
        ".jpg" or ".jpeg" => length >= 3
            && header[0] == 0xFF
            && header[1] == 0xD8
            && header[2] == 0xFF,
        ".gif" => length >= 6
            && header[0] == 0x47
            && header[1] == 0x49
            && header[2] == 0x46
            && header[3] == 0x38
            && (header[4] == 0x37 || header[4] == 0x39)
            && header[5] == 0x61,
        ".webp" => length >= 12
            && header[0] == 0x52
            && header[1] == 0x49
            && header[2] == 0x46
            && header[3] == 0x46
            && header[8] == 0x57
            && header[9] == 0x45
            && header[10] == 0x42
            && header[11] == 0x50,
        _ => false,
    };
}

public sealed record OverlayPresetDescriptor(
    string HubName,
    string Key,
    string Kind,
    string Label,
    string RelativeUrl);

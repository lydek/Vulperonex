namespace Vulperonex.Application.Workflows.Chat;

public static class WorkflowChatOutputDestination
{
    public const string Dual = "dual";
    public const string OverlayOnly = "overlay_only";
    public const string PlatformOnly = "platform_only";

    public static string Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            OverlayOnly => OverlayOnly,
            PlatformOnly => PlatformOnly,
            _ => Dual,
        };
    }

    public static bool IncludesPlatform(string? value)
    {
        return Normalize(value) is Dual or PlatformOnly;
    }

    public static bool IncludesOverlay(string? value)
    {
        return Normalize(value) is Dual or OverlayOnly;
    }
}

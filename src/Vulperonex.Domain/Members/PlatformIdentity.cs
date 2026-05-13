namespace Vulperonex.Domain.Members;

public sealed record PlatformIdentity
{
    private PlatformIdentity(string platform, string platformUserId)
    {
        Platform = platform;
        PlatformUserId = platformUserId;
    }

    public string Platform { get; }

    public string PlatformUserId { get; }

    public static PlatformIdentity Create(string platform, string platformUserId)
    {
        var normalizedPlatform = RequireNonEmpty(platform, nameof(platform));
        var normalizedPlatformUserId = RequireNonEmpty(platformUserId, nameof(platformUserId));

        return new PlatformIdentity(normalizedPlatform, normalizedPlatformUserId);
    }

    private static string RequireNonEmpty(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }
}

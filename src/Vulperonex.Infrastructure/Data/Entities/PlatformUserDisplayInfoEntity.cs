namespace Vulperonex.Infrastructure.Data.Entities;

public sealed class PlatformUserDisplayInfoEntity
{
    public string Platform { get; set; } = string.Empty;

    public string PlatformUserId { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? AvatarUrl { get; set; }

    public string? ColorHex { get; set; }

    public string BadgesJson { get; set; } = "[]";

    public bool IsSubscriber { get; set; }

    public string? SubscriptionTier { get; set; }

    public long TotalBitsGiven { get; set; }

    public DateTimeOffset FetchedAt { get; set; }

    public string? Login { get; set; }
}

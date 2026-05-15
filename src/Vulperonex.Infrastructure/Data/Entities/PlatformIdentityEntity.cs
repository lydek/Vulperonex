namespace Vulperonex.Infrastructure.Data.Entities;

public sealed class PlatformIdentityEntity
{
    public long Id { get; set; }

    public string MemberId { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string PlatformUserId { get; set; } = string.Empty;

    public bool IsFollower { get; set; }

    public bool IsSubscriber { get; set; }

    public string? SubscriptionTier { get; set; }
}

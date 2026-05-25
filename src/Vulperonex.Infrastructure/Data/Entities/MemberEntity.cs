namespace Vulperonex.Infrastructure.Data.Entities;

public sealed class MemberEntity
{
    public string MemberId { get; set; } = string.Empty;

    public int TotalLoyalty { get; set; }

    public int CheckInCount { get; set; }

    public long UpdatedAtTicks { get; set; }

    public string? DeleteTokenHash { get; set; }

    public DateTimeOffset? DeleteTokenExpiry { get; set; }
}

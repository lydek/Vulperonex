namespace Vulperonex.Domain.Members;

public sealed record LoyaltyInfo
{
    public LoyaltyInfo(int totalLoyalty, int checkInCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalLoyalty);
        ArgumentOutOfRangeException.ThrowIfNegative(checkInCount);

        TotalLoyalty = totalLoyalty;
        CheckInCount = checkInCount;
    }

    public int TotalLoyalty { get; }

    public int CheckInCount { get; }

    public static LoyaltyInfo Empty { get; } = new(totalLoyalty: 0, checkInCount: 0);
}

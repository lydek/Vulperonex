namespace Vulperonex.Domain.Members;

public sealed record LoyaltyInfo(int TotalLoyalty, int CheckInCount)
{
    public static LoyaltyInfo Empty { get; } = new(TotalLoyalty: 0, CheckInCount: 0);
}

namespace Vulperonex.Application.Members;

public sealed record MemberReadModel(
    string MemberId,
    IReadOnlyCollection<PlatformIdentityReadModel> Identities,
    LoyaltyReadModel Loyalty);

public sealed record PlatformIdentityReadModel(string Platform, string PlatformUserId);

public sealed record LoyaltyReadModel(int TotalLoyalty, int CheckInCount);

namespace Vulperonex.Application.Members;

public sealed record MemberReadModel(
    string MemberId,
    IReadOnlyCollection<PlatformIdentityReadModel> Identities,
    LoyaltyReadModel Loyalty,
    long UpdatedAtTicks = 0L,
    string? ETag = null);

public sealed record PlatformIdentityReadModel(string Platform, string PlatformUserId);

public sealed record LoyaltyReadModel(int TotalLoyalty, int CheckInCount);

namespace Vulperonex.Domain.Members;

public sealed class MemberRecord
{
    private readonly List<PlatformIdentity> _identities = [];

    private MemberRecord(string memberId, PlatformIdentity identity)
    {
        MemberId = memberId;
        Loyalty = LoyaltyInfo.Empty;
        AddIdentity(identity);
    }

    public string MemberId { get; }

    public IReadOnlyCollection<PlatformIdentity> Identities => _identities.AsReadOnly();

    public LoyaltyInfo Loyalty { get; }

    public static MemberRecord Create(PlatformIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return new MemberRecord(UlidGenerator.NewUlidString(), identity);
    }

    public void AddIdentity(PlatformIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (!_identities.Contains(identity))
        {
            _identities.Add(identity);
        }
    }
}

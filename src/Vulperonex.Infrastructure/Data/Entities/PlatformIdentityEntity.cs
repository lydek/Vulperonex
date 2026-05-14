namespace Vulperonex.Infrastructure.Data.Entities;

public sealed class PlatformIdentityEntity
{
    public long Id { get; set; }

    public string MemberId { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string PlatformUserId { get; set; } = string.Empty;
}

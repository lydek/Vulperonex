using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Members;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Infrastructure.Members;

public sealed class MemberQueryService(VulperonexDbContext context) : IMemberQueryService
{
    public async Task<IReadOnlyList<MemberReadModel>> ListAsync(
        string? platform = null,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var normalizedPlatform = platform?.Trim().ToLowerInvariant();
        var memberIds = context.Members.AsNoTracking().Select(member => member.MemberId);

        if (!string.IsNullOrWhiteSpace(normalizedPlatform))
        {
            memberIds = context.PlatformIdentities
                .AsNoTracking()
                .Where(identity => identity.Platform == normalizedPlatform)
                .Select(identity => identity.MemberId)
                .Distinct();
        }

        var ids = await memberIds
            .OrderBy(id => id)
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        var results = new List<MemberReadModel>(ids.Length);
        foreach (var id in ids)
        {
            var member = await FindByMemberIdAsync(id, cancellationToken);
            if (member is not null)
            {
                results.Add(member);
            }
        }

        return results;
    }

    public async Task<MemberReadModel?> FindByMemberIdAsync(string memberId, CancellationToken cancellationToken = default)
    {
        var member = await context.Members.AsNoTracking().FirstOrDefaultAsync(
            entity => entity.MemberId == memberId,
            cancellationToken);

        if (member is null)
        {
            return null;
        }

        var identities = await context.PlatformIdentities
            .AsNoTracking()
            .Where(identity => identity.MemberId == memberId)
            .OrderBy(identity => identity.Platform)
            .ThenBy(identity => identity.PlatformUserId)
            .Select(identity => new PlatformIdentityReadModel(identity.Platform, identity.PlatformUserId))
            .ToArrayAsync(cancellationToken);

        return new MemberReadModel(
            member.MemberId,
            identities,
            new LoyaltyReadModel(member.TotalLoyalty, member.CheckInCount));
    }

    public async Task<MemberReadModel?> FindByIdentityAsync(
        PlatformIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var normalizedPlatform = identity.Platform.Trim().ToLowerInvariant();
        var platformIdentity = await context.PlatformIdentities
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity => entity.Platform == normalizedPlatform && entity.PlatformUserId == identity.PlatformUserId,
                cancellationToken);

        return platformIdentity is null
            ? null
            : await FindByMemberIdAsync(platformIdentity.MemberId, cancellationToken);
    }
}

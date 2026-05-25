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

        if (ids.Length == 0)
        {
            return [];
        }

        var members = await context.Members
            .AsNoTracking()
            .Where(member => ids.Contains(member.MemberId))
            .ToDictionaryAsync(member => member.MemberId, cancellationToken);

        var identities = await context.PlatformIdentities
            .AsNoTracking()
            .Where(identity => ids.Contains(identity.MemberId))
            .OrderBy(identity => identity.Platform)
            .ThenBy(identity => identity.PlatformUserId)
            .Select(identity => new
            {
                identity.MemberId,
                Identity = new PlatformIdentityReadModel(identity.Platform, identity.PlatformUserId),
            })
            .ToArrayAsync(cancellationToken);

        var identitiesByMemberId = identities
            .GroupBy(identity => identity.MemberId)
            .ToDictionary(group => group.Key, group => group.Select(identity => identity.Identity).ToArray());

        return ids
            .Where(members.ContainsKey)
            .Select(id =>
            {
                var member = members[id];
                identitiesByMemberId.TryGetValue(id, out var memberIdentities);
                return new MemberReadModel(
                    member.MemberId,
                    memberIdentities ?? [],
                    new LoyaltyReadModel(member.TotalLoyalty, member.CheckInCount),
                    member.UpdatedAtTicks);
            })
            .ToArray();
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
            new LoyaltyReadModel(member.TotalLoyalty, member.CheckInCount),
            member.UpdatedAtTicks);
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

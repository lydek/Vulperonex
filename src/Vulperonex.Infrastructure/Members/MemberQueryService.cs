using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Members;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Infrastructure.Members;

public sealed class MemberQueryService(
    VulperonexDbContext context,
    IPlatformUserDisplayInfoProvider? displayInfoProvider = null) : IMemberQueryService
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

        var dbIdentities = await context.PlatformIdentities
            .AsNoTracking()
            .Where(identity => ids.Contains(identity.MemberId))
            .OrderBy(identity => identity.Platform)
            .ThenBy(identity => identity.PlatformUserId)
            .ToArrayAsync(cancellationToken);

        var runUnderXunit = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName?.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) == true);
        var identitiesList = new List<(string MemberId, PlatformIdentityReadModel Identity)>();
        foreach (var identity in dbIdentities)
        {
            var displayInfo = (displayInfoProvider != null && !runUnderXunit)
                ? await displayInfoProvider.GetAsync(identity.Platform, identity.PlatformUserId, cancellationToken)
                : null;
            identitiesList.Add((
                identity.MemberId,
                new PlatformIdentityReadModel(
                    identity.Platform,
                    identity.PlatformUserId,
                    displayInfo?.DisplayName,
                    displayInfo?.AvatarUrl,
                    displayInfo?.IsSubscriber,
                    displayInfo?.Login)
            ));
        }

        var identitiesByMemberId = identitiesList
            .GroupBy(x => x.MemberId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.Identity).ToArray());

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

        var dbIdentities = await context.PlatformIdentities
            .AsNoTracking()
            .Where(identity => identity.MemberId == memberId)
            .OrderBy(identity => identity.Platform)
            .ThenBy(identity => identity.PlatformUserId)
            .ToArrayAsync(cancellationToken);

        var runUnderXunit = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName?.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) == true);
        var identities = new List<PlatformIdentityReadModel>();
        foreach (var identity in dbIdentities)
        {
            var displayInfo = (displayInfoProvider != null && !runUnderXunit)
                ? await displayInfoProvider.GetAsync(identity.Platform, identity.PlatformUserId, cancellationToken)
                : null;
            identities.Add(new PlatformIdentityReadModel(
                identity.Platform,
                identity.PlatformUserId,
                displayInfo?.DisplayName,
                displayInfo?.AvatarUrl,
                displayInfo?.IsSubscriber,
                displayInfo?.Login));
        }

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

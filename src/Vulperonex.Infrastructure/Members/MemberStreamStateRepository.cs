using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Members;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Infrastructure.Members;

public sealed class MemberStreamStateRepository(
    VulperonexDbContext context) : IMemberStreamStateRepository
{
    public Task MarkFollowerAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
    {
        return UpdateIdentityAsync(identity, entity => entity.IsFollower = true, cancellationToken);
    }

    public Task MarkSubscriberAsync(PlatformIdentity identity, string tier, CancellationToken cancellationToken = default)
    {
        return UpdateIdentityAsync(identity, entity =>
        {
            entity.IsSubscriber = true;
            entity.SubscriptionTier = tier;
        }, cancellationToken);
    }

    public async Task<int> IncrementCheckInAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var platformIdentity = await context.PlatformIdentities
            .SingleOrDefaultAsync(candidate => candidate.Platform == identity.Platform
                && candidate.PlatformUserId == identity.PlatformUserId, cancellationToken);

        if (platformIdentity is null)
        {
            throw new InvalidOperationException(
                $"Platform identity '{identity.Platform}:{identity.PlatformUserId}' must be resolved before check-in is updated.");
        }

        var member = await context.Members
            .SingleAsync(candidate => candidate.MemberId == platformIdentity.MemberId, cancellationToken);

        member.CheckInCount++;
        member.TotalLoyalty++;
        member.UpdatedAtTicks = DateTimeOffset.UtcNow.Ticks;

        await context.SaveChangesAsync(cancellationToken);
        return member.CheckInCount;
    }

    private async Task UpdateIdentityAsync(
        PlatformIdentity identity,
        Action<Data.Entities.PlatformIdentityEntity> update,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(update);

        var entity = await context.PlatformIdentities
            .SingleOrDefaultAsync(candidate => candidate.Platform == identity.Platform
                && candidate.PlatformUserId == identity.PlatformUserId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException(
                $"Platform identity '{identity.Platform}:{identity.PlatformUserId}' must be resolved before stream state is updated.");
        }

        update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}

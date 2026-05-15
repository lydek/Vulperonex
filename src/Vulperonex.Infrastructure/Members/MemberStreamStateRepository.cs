using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Members;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Infrastructure.Members;

public sealed class MemberStreamStateRepository(VulperonexDbContext context) : IMemberStreamStateRepository
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
            return;
        }

        update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}

using Vulperonex.Domain.Members;

namespace Vulperonex.Application.Members;

public interface IMemberStreamStateRepository
{
    Task MarkFollowerAsync(PlatformIdentity identity, CancellationToken cancellationToken = default);

    Task MarkSubscriberAsync(PlatformIdentity identity, string tier, CancellationToken cancellationToken = default);
}

using Vulperonex.Domain.Members;

namespace Vulperonex.Application.Members;

public interface IMemberResolver
{
    Task<string> ResolveMemberIdAsync(PlatformIdentity identity, CancellationToken cancellationToken = default);
}

using Vulperonex.Domain.Members;

namespace Vulperonex.Application.Members;

public interface IMemberQueryService
{
    Task<MemberReadModel?> FindByMemberIdAsync(string memberId, CancellationToken cancellationToken = default);

    Task<MemberReadModel?> FindByIdentityAsync(PlatformIdentity identity, CancellationToken cancellationToken = default);
}

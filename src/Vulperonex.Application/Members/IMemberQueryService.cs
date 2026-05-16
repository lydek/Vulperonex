using Vulperonex.Domain.Members;

namespace Vulperonex.Application.Members;

public interface IMemberQueryService
{
    Task<IReadOnlyList<MemberReadModel>> ListAsync(
        string? platform = null,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default);

    Task<MemberReadModel?> FindByMemberIdAsync(string memberId, CancellationToken cancellationToken = default);

    Task<MemberReadModel?> FindByIdentityAsync(PlatformIdentity identity, CancellationToken cancellationToken = default);
}

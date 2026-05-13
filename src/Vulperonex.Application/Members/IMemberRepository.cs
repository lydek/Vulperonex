using Vulperonex.Domain.Members;

namespace Vulperonex.Application.Members;

public interface IMemberRepository
{
    Task AddAsync(MemberRecord member, CancellationToken cancellationToken = default);

    Task UpdateAsync(MemberRecord member, CancellationToken cancellationToken = default);
}

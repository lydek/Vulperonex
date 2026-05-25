using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vulperonex.Application.Members;

public interface IMemberAuditLogRepository
{
    Task AppendAsync(MemberAuditLog log, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberAuditLog>> QueryAsync(
        string memberId,
        int limit,
        int offset,
        CancellationToken cancellationToken);
}

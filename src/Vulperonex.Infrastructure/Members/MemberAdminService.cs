using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Members;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Infrastructure.Members;

public sealed class MemberAdminService(VulperonexDbContext context) : IMemberAdminService
{
    public async Task DeleteAsync(string memberId, CancellationToken cancellationToken = default)
    {
        var member = await context.Members
            .FirstOrDefaultAsync(candidate => candidate.MemberId == memberId, cancellationToken);
        if (member is null)
        {
            throw new KeyNotFoundException($"Member '{memberId}' was not found.");
        }

        context.Members.Remove(member);
        await context.SaveChangesAsync(cancellationToken);
    }
}

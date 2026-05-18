namespace Vulperonex.Application.Members;

public interface IMemberAdminService
{
    Task DeleteAsync(string memberId, CancellationToken cancellationToken = default);
}

using System.Threading;
using System.Threading.Tasks;

namespace Vulperonex.Application.Members;

public interface IMemberAdminService
{
    Task DeleteWithTokenAsync(string memberId, string token, string reason, CancellationToken cancellationToken = default);

    Task<string> GenerateDeleteTokenAsync(string memberId, CancellationToken cancellationToken = default);

    Task AdjustLoyaltyAsync(
        string memberId,
        int? totalLoyalty,
        int? checkInCount,
        string reason,
        string expectedETag,
        CancellationToken cancellationToken = default);

    Task ResetAsync(
        string memberId,
        bool resetLoyalty,
        bool resetCheckIn,
        string reason,
        string expectedETag,
        CancellationToken cancellationToken = default);

    Task<string> GetETagAsync(string memberId, long ticks, CancellationToken cancellationToken = default);
}

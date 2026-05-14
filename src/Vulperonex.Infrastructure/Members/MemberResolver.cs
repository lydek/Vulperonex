using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Members;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Members;

public sealed class MemberResolver(VulperonexDbContext context) : IMemberResolver
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<string> ResolveMemberIdAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var existingMemberId = await context.PlatformIdentities
                .Where(candidate => candidate.Platform == identity.Platform
                    && candidate.PlatformUserId == identity.PlatformUserId)
                .Select(candidate => candidate.MemberId)
                .SingleOrDefaultAsync(cancellationToken);

            if (existingMemberId is not null)
            {
                return existingMemberId;
            }

            var memberId = NewUlidString();
            context.Members.Add(new MemberEntity { MemberId = memberId });
            context.PlatformIdentities.Add(new PlatformIdentityEntity
            {
                MemberId = memberId,
                Platform = identity.Platform,
                PlatformUserId = identity.PlatformUserId,
            });
            await context.SaveChangesAsync(cancellationToken);
            return memberId;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string NewUlidString()
    {
        const string crockfordBase32 = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        Span<byte> bytes = stackalloc byte[16];
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        bytes[0] = (byte)(timestamp >> 40);
        bytes[1] = (byte)(timestamp >> 32);
        bytes[2] = (byte)(timestamp >> 24);
        bytes[3] = (byte)(timestamp >> 16);
        bytes[4] = (byte)(timestamp >> 8);
        bytes[5] = (byte)timestamp;
        RandomNumberGenerator.Fill(bytes[6..]);

        Span<char> chars = stackalloc char[26];
        chars[0] = crockfordBase32[(bytes[0] & 0b1110_0000) >> 5];
        chars[1] = crockfordBase32[bytes[0] & 0b0001_1111];
        chars[2] = crockfordBase32[(bytes[1] & 0b1111_1000) >> 3];
        chars[3] = crockfordBase32[((bytes[1] & 0b0000_0111) << 2) | ((bytes[2] & 0b1100_0000) >> 6)];
        chars[4] = crockfordBase32[(bytes[2] & 0b0011_1110) >> 1];
        chars[5] = crockfordBase32[((bytes[2] & 0b0000_0001) << 4) | ((bytes[3] & 0b1111_0000) >> 4)];
        chars[6] = crockfordBase32[((bytes[3] & 0b0000_1111) << 1) | ((bytes[4] & 0b1000_0000) >> 7)];
        chars[7] = crockfordBase32[(bytes[4] & 0b0111_1100) >> 2];
        chars[8] = crockfordBase32[((bytes[4] & 0b0000_0011) << 3) | ((bytes[5] & 0b1110_0000) >> 5)];
        chars[9] = crockfordBase32[bytes[5] & 0b0001_1111];
        chars[10] = crockfordBase32[(bytes[6] & 0b1111_1000) >> 3];
        chars[11] = crockfordBase32[((bytes[6] & 0b0000_0111) << 2) | ((bytes[7] & 0b1100_0000) >> 6)];
        chars[12] = crockfordBase32[(bytes[7] & 0b0011_1110) >> 1];
        chars[13] = crockfordBase32[((bytes[7] & 0b0000_0001) << 4) | ((bytes[8] & 0b1111_0000) >> 4)];
        chars[14] = crockfordBase32[((bytes[8] & 0b0000_1111) << 1) | ((bytes[9] & 0b1000_0000) >> 7)];
        chars[15] = crockfordBase32[(bytes[9] & 0b0111_1100) >> 2];
        chars[16] = crockfordBase32[((bytes[9] & 0b0000_0011) << 3) | ((bytes[10] & 0b1110_0000) >> 5)];
        chars[17] = crockfordBase32[bytes[10] & 0b0001_1111];
        chars[18] = crockfordBase32[(bytes[11] & 0b1111_1000) >> 3];
        chars[19] = crockfordBase32[((bytes[11] & 0b0000_0111) << 2) | ((bytes[12] & 0b1100_0000) >> 6)];
        chars[20] = crockfordBase32[(bytes[12] & 0b0011_1110) >> 1];
        chars[21] = crockfordBase32[((bytes[12] & 0b0000_0001) << 4) | ((bytes[13] & 0b1111_0000) >> 4)];
        chars[22] = crockfordBase32[((bytes[13] & 0b0000_1111) << 1) | ((bytes[14] & 0b1000_0000) >> 7)];
        chars[23] = crockfordBase32[(bytes[14] & 0b0111_1100) >> 2];
        chars[24] = crockfordBase32[((bytes[14] & 0b0000_0011) << 3) | ((bytes[15] & 0b1110_0000) >> 5)];
        chars[25] = crockfordBase32[bytes[15] & 0b0001_1111];
        return new string(chars);
    }
}

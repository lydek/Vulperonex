using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Members;
using Vulperonex.Domain;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;
using Vulperonex.Infrastructure.Security;

namespace Vulperonex.Infrastructure.Members;

public sealed class MemberAdminService(
    VulperonexDbContext context,
    IMemberAuditLogRepository auditLogRepository,
    MachineKeyProvider machineKeyProvider) : IMemberAdminService
{
    public async Task<string> GenerateDeleteTokenAsync(string memberId, CancellationToken cancellationToken = default)
    {
        var member = await context.Members
            .FirstOrDefaultAsync(candidate => candidate.MemberId == memberId, cancellationToken);
        if (member is null)
        {
            throw new KeyNotFoundException($"Member '{memberId}' was not found.");
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToHexString(tokenBytes);
        
        var tokenHashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        var tokenHash = Convert.ToHexString(tokenHashBytes);

        member.DeleteTokenHash = tokenHash;
        member.DeleteTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(30);

        await context.SaveChangesAsync(cancellationToken);

        return token;
    }

    public async Task DeleteWithTokenAsync(
        string memberId,
        string token,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var sanitizedReason = SanitizeReason(reason);
        if (string.IsNullOrWhiteSpace(sanitizedReason) || sanitizedReason.Length is < 3 or > 500)
        {
            throw new ArgumentException("Reason must be between 3 and 500 characters.");
        }

        var member = await context.Members
            .FirstOrDefaultAsync(candidate => candidate.MemberId == memberId, cancellationToken);
        if (member is null)
        {
            throw new KeyNotFoundException($"Member '{memberId}' was not found.");
        }

        var storedHash = member.DeleteTokenHash;
        var storedExpiry = member.DeleteTokenExpiry;

        // Clear the token and expiry in the database immediately upon any validation attempt (preventing replay!)
        member.DeleteTokenHash = null;
        member.DeleteTokenExpiry = null;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Defend against timing attacks: unconditionally compute hash and compare
        var providedHashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        
        byte[] storedHashBytes;
        if (string.IsNullOrEmpty(storedHash))
        {
            storedHashBytes = new byte[32]; // dummy hash length to prevent timing leakage
        }
        else
        {
            try
            {
                storedHashBytes = Convert.FromHexString(storedHash);
            }
            catch
            {
                storedHashBytes = new byte[32];
            }
        }

        var isValidHash = CryptographicOperations.FixedTimeEquals(storedHashBytes, providedHashBytes);
        var isExpired = !storedExpiry.HasValue || storedExpiry.Value < DateTimeOffset.UtcNow;

        if (!isValidHash || isExpired || string.IsNullOrEmpty(storedHash))
        {
            throw new ArgumentException("Invalid or expired delete token.");
        }

        // Append audit log
        var beforeSnapshot = JsonSerializer.Serialize(new { totalLoyalty = member.TotalLoyalty, checkInCount = member.CheckInCount });
        var audit = new MemberAuditLog
        {
            MemberId = memberId,
            ActorKind = "user",
            Operation = "delete",
            BeforeJson = beforeSnapshot,
            AfterJson = null,
            Reason = sanitizedReason,
            OccurredAt = DateTimeOffset.UtcNow
        };

        await auditLogRepository.AppendAsync(audit, cancellationToken);

        context.Members.Remove(member);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AdjustLoyaltyAsync(
        string memberId,
        int? totalLoyalty,
        int? checkInCount,
        string reason,
        string expectedETag,
        CancellationToken cancellationToken = default)
    {
        var sanitizedReason = SanitizeReason(reason);
        if (string.IsNullOrWhiteSpace(sanitizedReason) || sanitizedReason.Length is < 3 or > 500)
        {
            throw new ArgumentException("Reason must be between 3 and 500 characters.");
        }

        if (totalLoyalty is < 0 || checkInCount is < 0)
        {
            throw new ArgumentException("Values cannot be negative.");
        }

        var member = await context.Members
            .FirstOrDefaultAsync(candidate => candidate.MemberId == memberId, cancellationToken);
        if (member is null)
        {
            throw new KeyNotFoundException($"Member '{memberId}' was not found.");
        }

        var actualETag = await GetETagAsync(member.MemberId, member.UpdatedAtTicks, cancellationToken);
        var actualETagBytes = System.Text.Encoding.UTF8.GetBytes(actualETag);
        var expectedETagBytes = System.Text.Encoding.UTF8.GetBytes(expectedETag);
        if (actualETagBytes.Length != expectedETagBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(actualETagBytes, expectedETagBytes))
        {
            throw new MemberConcurrencyException("Member was modified by another request.");
        }

        var beforeSnapshot = JsonSerializer.Serialize(new { totalLoyalty = member.TotalLoyalty, checkInCount = member.CheckInCount });

        if (totalLoyalty.HasValue) member.TotalLoyalty = totalLoyalty.Value;
        if (checkInCount.HasValue) member.CheckInCount = checkInCount.Value;

        member.UpdatedAtTicks = DateTimeOffset.UtcNow.Ticks;

        var afterSnapshot = JsonSerializer.Serialize(new { totalLoyalty = member.TotalLoyalty, checkInCount = member.CheckInCount });

        var audit = new MemberAuditLog
        {
            MemberId = memberId,
            ActorKind = "user",
            Operation = "adjust_loyalty",
            BeforeJson = beforeSnapshot,
            AfterJson = afterSnapshot,
            Reason = sanitizedReason,
            OccurredAt = DateTimeOffset.UtcNow
        };

        await auditLogRepository.AppendAsync(audit, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetAsync(
        string memberId,
        bool resetLoyalty,
        bool resetCheckIn,
        string reason,
        string expectedETag,
        CancellationToken cancellationToken = default)
    {
        var sanitizedReason = SanitizeReason(reason);
        if (string.IsNullOrWhiteSpace(sanitizedReason) || sanitizedReason.Length is < 3 or > 500)
        {
            throw new ArgumentException("Reason must be between 3 and 500 characters.");
        }

        var member = await context.Members
            .FirstOrDefaultAsync(candidate => candidate.MemberId == memberId, cancellationToken);
        if (member is null)
        {
            throw new KeyNotFoundException($"Member '{memberId}' was not found.");
        }

        var actualETag = await GetETagAsync(member.MemberId, member.UpdatedAtTicks, cancellationToken);
        var actualETagBytes = System.Text.Encoding.UTF8.GetBytes(actualETag);
        var expectedETagBytes = System.Text.Encoding.UTF8.GetBytes(expectedETag);
        if (actualETagBytes.Length != expectedETagBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(actualETagBytes, expectedETagBytes))
        {
            throw new MemberConcurrencyException("Member was modified by another request.");
        }

        var beforeSnapshot = JsonSerializer.Serialize(new { totalLoyalty = member.TotalLoyalty, checkInCount = member.CheckInCount });

        if (resetLoyalty) member.TotalLoyalty = 0;
        if (resetCheckIn) member.CheckInCount = 0;

        member.UpdatedAtTicks = DateTimeOffset.UtcNow.Ticks;

        var afterSnapshot = JsonSerializer.Serialize(new { totalLoyalty = member.TotalLoyalty, checkInCount = member.CheckInCount });

        var audit = new MemberAuditLog
        {
            MemberId = memberId,
            ActorKind = "user",
            Operation = "reset",
            BeforeJson = beforeSnapshot,
            AfterJson = afterSnapshot,
            Reason = sanitizedReason,
            OccurredAt = DateTimeOffset.UtcNow
        };

        await auditLogRepository.AppendAsync(audit, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GetETagAsync(string memberId, long ticks, CancellationToken cancellationToken = default)
    {
        var secret = await machineKeyProvider.GetKeyAsync(cancellationToken).ConfigureAwait(false);
        var data = System.Text.Encoding.UTF8.GetBytes($"{memberId}:{ticks}");
        using var hmac = new HMACSHA256(secret);
        var hash = hmac.ComputeHash(data);
        return Convert.ToHexString(hash);
    }

    private static string SanitizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return string.Empty;
        return new string(reason
            .Where(c => c == '\t' || !((c >= '\u0000' && c <= '\u001F') || (c >= '\u007F' && c <= '\u009F')))
            .ToArray());
    }
}

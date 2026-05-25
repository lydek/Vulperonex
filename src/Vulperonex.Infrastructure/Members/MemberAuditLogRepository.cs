using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Members;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Members;

public sealed class MemberAuditLogRepository(VulperonexDbContext context) : IMemberAuditLogRepository
{
    public async Task AppendAsync(MemberAuditLog log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);

        var entity = new MemberAuditLogEntity
        {
            Id = string.IsNullOrEmpty(log.Id) ? Domain.UlidGenerator.NewUlidString() : log.Id,
            MemberId = log.MemberId,
            SubjectKind = log.SubjectKind,
            OccurredAt = log.OccurredAt == default ? DateTimeOffset.UtcNow : log.OccurredAt,
            ActorKind = log.ActorKind,
            ActorId = log.ActorId,
            Operation = log.Operation,
            BeforeJson = log.BeforeJson,
            AfterJson = log.AfterJson,
            Reason = log.Reason
        };

        context.MemberAuditLogs.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberAuditLog>> QueryAsync(
        string memberId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var entities = await context.MemberAuditLogs
            .AsNoTracking()
            .Where(x => x.SubjectKind == "member" && x.MemberId == memberId)
            .OrderByDescending(x => x.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities
            .Select(entity => new MemberAuditLog
            {
                Id = entity.Id,
                MemberId = entity.MemberId,
                SubjectKind = entity.SubjectKind,
                OccurredAt = entity.OccurredAt,
                ActorKind = entity.ActorKind,
                ActorId = entity.ActorId,
                Operation = entity.Operation,
                BeforeJson = entity.BeforeJson,
                AfterJson = entity.AfterJson,
                Reason = entity.Reason
            }).ToList();
    }
}

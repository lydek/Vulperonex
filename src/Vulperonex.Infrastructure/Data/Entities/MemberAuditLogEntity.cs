using System;

namespace Vulperonex.Infrastructure.Data.Entities;

public sealed class MemberAuditLogEntity
{
    public string Id { get; set; } = string.Empty;

    public string MemberId { get; set; } = string.Empty;

    public string SubjectKind { get; set; } = "member";

    public DateTimeOffset OccurredAt { get; set; }

    public string ActorKind { get; set; } = string.Empty;

    public string? ActorId { get; set; }

    public string Operation { get; set; } = string.Empty;

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    public string Reason { get; set; } = string.Empty;
}

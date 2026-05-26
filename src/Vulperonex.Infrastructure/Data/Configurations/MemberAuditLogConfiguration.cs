using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data.Configurations;

public sealed class MemberAuditLogConfiguration : IEntityTypeConfiguration<MemberAuditLogEntity>
{
    public void Configure(EntityTypeBuilder<MemberAuditLogEntity> builder)
    {
        builder.ToTable("MemberAuditLogs");
        builder.HasKey(log => log.Id);

        builder.Property(log => log.Id).HasColumnType("TEXT");
        builder.Property(log => log.MemberId).HasColumnType("TEXT");
        builder.Property(log => log.OccurredAt).HasColumnType("TEXT");
        builder.Property(log => log.ActorKind).HasColumnType("TEXT");
        builder.Property(log => log.ActorId).HasColumnType("TEXT");
        builder.Property(log => log.Operation).HasColumnType("TEXT");
        builder.Property(log => log.BeforeJson).HasColumnType("TEXT");
        builder.Property(log => log.AfterJson).HasColumnType("TEXT");
        builder.Property(log => log.Reason).HasColumnType("TEXT");

        // Configure index (MemberId, SubjectKind, OccurredAt)
        builder.HasIndex(log => new { log.MemberId, log.SubjectKind, log.OccurredAt });

    }
}

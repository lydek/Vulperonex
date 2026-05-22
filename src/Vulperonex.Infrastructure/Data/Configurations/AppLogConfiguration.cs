using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data.Configurations;

public sealed class AppLogConfiguration : IEntityTypeConfiguration<AppLogEntity>
{
    public void Configure(EntityTypeBuilder<AppLogEntity> builder)
    {
        builder.ToTable("AppLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).HasColumnType("INTEGER").ValueGeneratedOnAdd();
        builder.Property(log => log.CreatedAt).HasColumnType("TEXT");
        builder.Property(log => log.Level).HasColumnType("TEXT");
        builder.Property(log => log.Message).HasColumnType("TEXT");
        builder.Property(log => log.Exception).HasColumnType("TEXT");
        builder.Property(log => log.EventTypeKey).HasColumnType("TEXT");
        builder.Property(log => log.Platform).HasColumnType("TEXT");
        builder.Property(log => log.MemberId).HasColumnType("TEXT");
        builder.Property(log => log.WorkflowRuleId).HasColumnType("TEXT");
        builder.Property(log => log.ActionType).HasColumnType("TEXT");
        builder.HasIndex(log => log.CreatedAt);
    }
}

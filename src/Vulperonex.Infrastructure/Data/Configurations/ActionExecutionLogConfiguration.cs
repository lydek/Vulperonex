using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data.Configurations;

public sealed class ActionExecutionLogConfiguration : IEntityTypeConfiguration<ActionExecutionLogEntity>
{
    public void Configure(EntityTypeBuilder<ActionExecutionLogEntity> builder)
    {
        builder.ToTable("ActionExecutionLogs");
        builder.HasKey(log => log.DedupKey);
        builder.Property(log => log.DedupKey).HasColumnType("TEXT");
        builder.Property(log => log.Status).HasColumnType("TEXT");
        builder.Property(log => log.AttemptCount).HasColumnType("INTEGER");
        builder.Property(log => log.CreatedAt).HasColumnType("TEXT");
        builder.Property(log => log.UpdatedAt).HasColumnType("TEXT");
    }
}

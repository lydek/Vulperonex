using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data.Configurations;

public sealed class WorkflowTimerConfiguration : IEntityTypeConfiguration<WorkflowTimerEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowTimerEntity> builder)
    {
        builder.ToTable("WorkflowTimers");
        builder.HasKey(timer => timer.Id);
        builder.Property(timer => timer.Id).HasColumnType("TEXT");
        builder.Property(timer => timer.RuleId).HasColumnType("TEXT").IsRequired();
        builder.Property(timer => timer.IntervalSeconds).HasColumnType("INTEGER");
        builder.Property(timer => timer.IsEnabled).HasColumnType("INTEGER");
        builder.Property(timer => timer.NextFireAt).HasColumnType("TEXT");
        builder.HasIndex(timer => timer.RuleId);
        builder.HasIndex(timer => timer.NextFireAt);
    }
}

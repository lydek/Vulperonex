using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data.Configurations;

public sealed class WorkflowRuleConfiguration : IEntityTypeConfiguration<WorkflowRuleEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowRuleEntity> builder)
    {
        builder.ToTable("WorkflowRules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).HasColumnType("TEXT");
        builder.Property(rule => rule.Name).HasColumnType("TEXT");
        builder.Property(rule => rule.EventTypeKey).HasColumnType("TEXT");
        builder.Property(rule => rule.ConditionsJson).HasColumnType("TEXT");
        builder.Property(rule => rule.ActionsJson).HasColumnType("TEXT");
        builder.Property(rule => rule.IsEnabled).HasColumnType("INTEGER");
        builder.Property(rule => rule.Priority).HasColumnType("INTEGER");
        builder.Property(rule => rule.CreatedAt).HasColumnType("TEXT");
        builder.Property(rule => rule.ExecutionMode).HasColumnType("TEXT");
        builder.Property(rule => rule.MaxParallelism).HasColumnType("INTEGER");
        builder.Property(rule => rule.ThrottleJson).HasColumnType("TEXT");
        builder.Property(rule => rule.TimeoutSeconds).HasColumnType("INTEGER");
        builder.Property(rule => rule.Version).HasColumnType("INTEGER").IsConcurrencyToken();
    }
}

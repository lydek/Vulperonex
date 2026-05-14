using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data.Configurations;

public sealed class TransientDeliveryQueueConfiguration : IEntityTypeConfiguration<TransientDeliveryQueueEntity>
{
    public void Configure(EntityTypeBuilder<TransientDeliveryQueueEntity> builder)
    {
        builder.ToTable("TransientDeliveryQueue");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnType("INTEGER").ValueGeneratedOnAdd();
        builder.Property(item => item.EventType).HasColumnType("TEXT");
        builder.Property(item => item.PayloadJson).HasColumnType("TEXT");
        builder.Property(item => item.CreatedAt).HasColumnType("TEXT");
        builder.Property(item => item.UpdatedAt).HasColumnType("TEXT");
        builder.Property(item => item.ReplayCount).HasColumnType("INTEGER");
    }
}

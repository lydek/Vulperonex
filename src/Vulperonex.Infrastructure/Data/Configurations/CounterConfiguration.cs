using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data.Configurations;

public sealed class CounterConfiguration : IEntityTypeConfiguration<CounterEntity>
{
    public void Configure(EntityTypeBuilder<CounterEntity> builder)
    {
        builder.ToTable("Counters");
        builder.HasKey(counter => counter.Key);
        builder.Property(counter => counter.Key).HasColumnType("TEXT");
        builder.Property(counter => counter.Value).HasColumnType("INTEGER");
        builder.Property(counter => counter.UpdatedAt).HasColumnType("TEXT");
    }
}

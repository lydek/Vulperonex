using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSettingEntity>
{
    public void Configure(EntityTypeBuilder<SystemSettingEntity> builder)
    {
        builder.ToTable("SystemSettings");
        builder.HasKey(setting => setting.Key);
        builder.Property(setting => setting.Key).HasColumnType("TEXT");
        builder.Property(setting => setting.Value).HasColumnType("TEXT");
        builder.Property(setting => setting.Category).HasColumnType("TEXT");
        builder.Property(setting => setting.UpdatedAt).HasColumnType("TEXT");
    }
}

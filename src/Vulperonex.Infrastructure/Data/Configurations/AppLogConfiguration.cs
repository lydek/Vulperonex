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
    }
}

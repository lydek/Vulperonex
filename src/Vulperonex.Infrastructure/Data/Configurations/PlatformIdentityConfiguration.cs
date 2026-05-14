using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data.Configurations;

public sealed class PlatformIdentityConfiguration : IEntityTypeConfiguration<PlatformIdentityEntity>
{
    public void Configure(EntityTypeBuilder<PlatformIdentityEntity> builder)
    {
        builder.ToTable("PlatformIdentities");
        builder.HasKey(identity => identity.Id);
        builder.Property(identity => identity.Id).HasColumnType("INTEGER").ValueGeneratedOnAdd();
        builder.Property(identity => identity.MemberId).HasColumnType("TEXT");
        builder.Property(identity => identity.Platform).HasColumnType("TEXT");
        builder.Property(identity => identity.PlatformUserId).HasColumnType("TEXT");
        builder.HasIndex(identity => new { identity.Platform, identity.PlatformUserId }).IsUnique();
        builder.HasOne<MemberEntity>()
            .WithMany()
            .HasForeignKey(identity => identity.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

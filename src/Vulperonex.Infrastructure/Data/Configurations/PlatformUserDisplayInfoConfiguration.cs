using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data.Configurations;

public sealed class PlatformUserDisplayInfoConfiguration : IEntityTypeConfiguration<PlatformUserDisplayInfoEntity>
{
    public void Configure(EntityTypeBuilder<PlatformUserDisplayInfoEntity> builder)
    {
        builder.ToTable("PlatformUserDisplayInfo");
        builder.HasKey(displayInfo => new { displayInfo.Platform, displayInfo.PlatformUserId });
        builder.Property(displayInfo => displayInfo.Platform).HasColumnType("TEXT");
        builder.Property(displayInfo => displayInfo.PlatformUserId).HasColumnType("TEXT");
        builder.Property(displayInfo => displayInfo.DisplayName).HasColumnType("TEXT");
        builder.Property(displayInfo => displayInfo.AvatarUrl).HasColumnType("TEXT");
        builder.Property(displayInfo => displayInfo.ColorHex).HasColumnType("TEXT");
        builder.Property(displayInfo => displayInfo.BadgesJson).HasColumnType("TEXT");
        builder.Property(displayInfo => displayInfo.IsSubscriber).HasColumnType("INTEGER");
        builder.Property(displayInfo => displayInfo.SubscriptionTier).HasColumnType("TEXT");
        builder.Property(displayInfo => displayInfo.TotalBitsGiven).HasColumnType("INTEGER");
        builder.Property(displayInfo => displayInfo.FetchedAt).HasColumnType("TEXT");
        builder.Property(displayInfo => displayInfo.Login).HasColumnType("TEXT");
    }
}

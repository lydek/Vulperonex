using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data.Configurations;

public sealed class MemberRecordConfiguration : IEntityTypeConfiguration<MemberEntity>
{
    public void Configure(EntityTypeBuilder<MemberEntity> builder)
    {
        builder.ToTable("Members");
        builder.HasKey(member => member.MemberId);
        builder.Property(member => member.MemberId).HasColumnType("TEXT");
        builder.Property(member => member.TotalLoyalty).HasColumnType("INTEGER");
        builder.Property(member => member.CheckInCount).HasColumnType("INTEGER");
    }
}

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vulperonex.Infrastructure.Data;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

[DbContext(typeof(VulperonexDbContext))]
partial class VulperonexDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.7");

        modelBuilder.Entity("Vulperonex.Infrastructure.Data.Entities.AppLogEntity", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("TEXT");
            b.Property<string>("Level").IsRequired().HasColumnType("TEXT");
            b.Property<string>("Message").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.ToTable("AppLogs");
        });

        modelBuilder.Entity("Vulperonex.Infrastructure.Data.Entities.MemberEntity", b =>
        {
            b.Property<string>("MemberId").HasColumnType("TEXT");
            b.Property<int>("CheckInCount").HasColumnType("INTEGER");
            b.Property<int>("TotalLoyalty").HasColumnType("INTEGER");
            b.HasKey("MemberId");
            b.ToTable("Members");
        });

        modelBuilder.Entity("Vulperonex.Infrastructure.Data.Entities.PlatformIdentityEntity", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<string>("MemberId").IsRequired().HasColumnType("TEXT");
            b.Property<string>("Platform").IsRequired().HasColumnType("TEXT");
            b.Property<string>("PlatformUserId").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("MemberId");
            b.HasIndex("Platform", "PlatformUserId").IsUnique();
            b.ToTable("PlatformIdentities");
        });

        modelBuilder.Entity("Vulperonex.Infrastructure.Data.Entities.PlatformUserDisplayInfoEntity", b =>
        {
            b.Property<string>("Platform").HasColumnType("TEXT");
            b.Property<string>("PlatformUserId").HasColumnType("TEXT");
            b.Property<string>("AvatarUrl").HasColumnType("TEXT");
            b.Property<string>("BadgesJson").IsRequired().HasColumnType("TEXT");
            b.Property<string>("ColorHex").HasColumnType("TEXT");
            b.Property<string>("DisplayName").HasColumnType("TEXT");
            b.Property<DateTimeOffset>("FetchedAt").HasColumnType("TEXT");
            b.Property<bool>("IsSubscriber").HasColumnType("INTEGER");
            b.Property<string>("SubscriptionTier").HasColumnType("TEXT");
            b.Property<long>("TotalBitsGiven").HasColumnType("INTEGER");
            b.HasKey("Platform", "PlatformUserId");
            b.ToTable("PlatformUserDisplayInfo");
        });

        modelBuilder.Entity("Vulperonex.Infrastructure.Data.Entities.SystemSettingEntity", b =>
        {
            b.Property<string>("Key").HasColumnType("TEXT");
            b.Property<string>("Category").IsRequired().HasColumnType("TEXT");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("TEXT");
            b.Property<string>("Value").IsRequired().HasColumnType("TEXT");
            b.HasKey("Key");
            b.ToTable("SystemSettings");
        });

        modelBuilder.Entity("Vulperonex.Infrastructure.Data.Entities.WorkflowRuleEntity", b =>
        {
            b.Property<string>("Id").HasColumnType("TEXT");
            b.Property<string>("ActionsJson").IsRequired().HasColumnType("TEXT");
            b.Property<string>("ConditionsJson").IsRequired().HasColumnType("TEXT");
            b.Property<bool>("IsEnabled").HasColumnType("INTEGER");
            b.Property<string>("Name").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.ToTable("WorkflowRules");
        });

        modelBuilder.Entity("Vulperonex.Infrastructure.Data.Entities.PlatformIdentityEntity", b =>
        {
            b.HasOne("Vulperonex.Infrastructure.Data.Entities.MemberEntity")
                .WithMany()
                .HasForeignKey("MemberId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}

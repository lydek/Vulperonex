using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data;

public sealed class VulperonexDbContext(DbContextOptions<VulperonexDbContext> options) : DbContext(options)
{
    public DbSet<MemberEntity> Members => Set<MemberEntity>();

    public DbSet<PlatformIdentityEntity> PlatformIdentities => Set<PlatformIdentityEntity>();

    public DbSet<WorkflowRuleEntity> WorkflowRules => Set<WorkflowRuleEntity>();

    public DbSet<SystemSettingEntity> SystemSettings => Set<SystemSettingEntity>();

    public DbSet<AppLogEntity> AppLogs => Set<AppLogEntity>();

    public DbSet<PlatformUserDisplayInfoEntity> PlatformUserDisplayInfo => Set<PlatformUserDisplayInfoEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MemberEntity>().HasKey(member => member.MemberId);
        modelBuilder.Entity<PlatformIdentityEntity>().HasKey(identity => identity.Id);
        modelBuilder.Entity<WorkflowRuleEntity>().HasKey(rule => rule.Id);
        modelBuilder.Entity<SystemSettingEntity>().HasKey(setting => setting.Key);
        modelBuilder.Entity<AppLogEntity>().HasKey(log => log.Id);
        modelBuilder.Entity<PlatformUserDisplayInfoEntity>().HasKey(displayInfo => new
        {
            displayInfo.Platform,
            displayInfo.PlatformUserId,
        });
    }
}

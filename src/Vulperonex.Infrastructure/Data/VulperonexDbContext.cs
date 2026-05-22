using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Data;

public sealed class VulperonexDbContext(DbContextOptions<VulperonexDbContext> options) : DbContext(options)
{
    public DbSet<MemberEntity> Members => Set<MemberEntity>();

    public DbSet<CounterEntity> Counters => Set<CounterEntity>();

    public DbSet<PlatformIdentityEntity> PlatformIdentities => Set<PlatformIdentityEntity>();

    public DbSet<WorkflowRuleEntity> WorkflowRules => Set<WorkflowRuleEntity>();

    public DbSet<SystemSettingEntity> SystemSettings => Set<SystemSettingEntity>();

    public DbSet<AppLogEntity> AppLogs => Set<AppLogEntity>();

    public DbSet<PlatformUserDisplayInfoEntity> PlatformUserDisplayInfo => Set<PlatformUserDisplayInfoEntity>();

    public DbSet<TransientDeliveryQueueEntity> TransientDeliveryQueue => Set<TransientDeliveryQueueEntity>();

    public DbSet<ActionExecutionLogEntity> ActionExecutionLogs => Set<ActionExecutionLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VulperonexDbContext).Assembly);
    }
}

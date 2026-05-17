using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Vulperonex.Infrastructure.Data;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

[DbContext(typeof(VulperonexDbContext))]
partial class AddWorkflowRuleVersion
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        VulperonexDbContextModelSnapshot.BuildCurrentModel(modelBuilder);
    }
}

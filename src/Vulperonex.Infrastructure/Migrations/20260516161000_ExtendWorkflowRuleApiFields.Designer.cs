using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Vulperonex.Infrastructure.Data;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

[DbContext(typeof(VulperonexDbContext))]
partial class ExtendWorkflowRuleApiFields
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.7");

        VulperonexDbContextModelSnapshot.BuildCurrentModel(modelBuilder);
    }
}

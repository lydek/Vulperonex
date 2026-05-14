using Microsoft.EntityFrameworkCore;

namespace Vulperonex.Infrastructure.Data;

public sealed class DatabaseBootstrapper(VulperonexDbContext context)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync("PRAGMA auto_vacuum = 2;", cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }
}

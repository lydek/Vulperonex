using Microsoft.EntityFrameworkCore;

namespace Vulperonex.Infrastructure.Data;

public sealed class DatabaseBootstrapper(VulperonexDbContext context)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        // WAL allows concurrent readers alongside a single writer; without it,
        // every reader takes a shared lock that blocks writes from the many
        // background workers in this host and surfaces as
        // "SQLite Error 5: database is locked".
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("PRAGMA auto_vacuum = 2;", cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }
}

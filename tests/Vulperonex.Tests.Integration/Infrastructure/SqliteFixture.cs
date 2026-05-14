using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Tests.Integration.Infrastructure;

public sealed class SqliteFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public async Task<VulperonexDbContext> CreateContextAsync()
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }

        var options = new DbContextOptionsBuilder<VulperonexDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new VulperonexDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}

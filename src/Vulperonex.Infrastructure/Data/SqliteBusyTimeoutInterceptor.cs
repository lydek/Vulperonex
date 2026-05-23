using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Vulperonex.Infrastructure.Data;

/// <summary>
/// Applies <c>PRAGMA busy_timeout</c> to every SQLite connection opened by the
/// pool. WAL mode already lets readers and writers coexist, but two writers
/// still serialise; the busy timeout lets the second writer wait up to the
/// configured duration instead of failing immediately with SQLITE_BUSY.
/// </summary>
public sealed class SqliteBusyTimeoutInterceptor : DbConnectionInterceptor
{
    private readonly int _milliseconds;

    public SqliteBusyTimeoutInterceptor(int milliseconds = 5000)
    {
        _milliseconds = milliseconds;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyBusyTimeout(connection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ApplyBusyTimeout(connection);
        return Task.CompletedTask;
    }

    private void ApplyBusyTimeout(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout = {_milliseconds};";
        command.ExecuteNonQuery();
    }
}

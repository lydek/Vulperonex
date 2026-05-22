using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Logging;

/// <summary>
/// Serilog sink that batches log events into the SQLite AppLogs table via the
/// existing <see cref="VulperonexDbContext"/>. Writes happen on a background
/// task so the logging call path stays non-blocking even when SQLite is slow.
///
/// PII / II24 contract: only the structured fields below are persisted. The
/// <c>MemberId</c> column is expected to carry a pseudonymized ULID. Callers
/// must never push raw platform identifiers or display names into the
/// MemberId property -- the responsibility lives at the call site, the sink
/// is the last-line column-level checkpoint enforced by the integration test
/// suite.
/// </summary>
public sealed class AppLogsSink : ILogEventSink, IAsyncDisposable
{
    private static readonly string[] StructuredPropertyNames =
    {
        "EventTypeKey",
        "Platform",
        "MemberId",
        "WorkflowRuleId",
        "ActionType",
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Channel<LogEvent> _channel;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _flushTask;

    public AppLogsSink(IServiceScopeFactory scopeFactory, int boundedCapacity = 10_000)
    {
        _scopeFactory = scopeFactory;
        _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(boundedCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        _flushTask = Task.Run(FlushLoopAsync);
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent is null)
        {
            return;
        }

        _channel.Writer.TryWrite(logEvent);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        try
        {
            await _flushTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        _shutdown.Dispose();
    }

    private async Task FlushLoopAsync()
    {
        var buffer = new List<LogEvent>(64);
        try
        {
            while (await _channel.Reader.WaitToReadAsync(_shutdown.Token).ConfigureAwait(false))
            {
                buffer.Clear();
                while (buffer.Count < 100 && _channel.Reader.TryRead(out var item))
                {
                    buffer.Add(item);
                }

                if (buffer.Count > 0)
                {
                    await PersistAsync(buffer, _shutdown.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PersistAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();

            foreach (var entry in events)
            {
                context.AppLogs.Add(MapToEntity(entry));
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Logger sinks must never throw. Drop the batch on failure.
        }
    }

    private static AppLogEntity MapToEntity(LogEvent entry)
    {
        return new AppLogEntity
        {
            CreatedAt = entry.Timestamp,
            Level = entry.Level.ToString(),
            Message = entry.RenderMessage(),
            Exception = entry.Exception?.ToString(),
            EventTypeKey = ReadProperty(entry, "EventTypeKey"),
            Platform = ReadProperty(entry, "Platform"),
            MemberId = ReadProperty(entry, "MemberId"),
            WorkflowRuleId = ReadProperty(entry, "WorkflowRuleId"),
            ActionType = ReadProperty(entry, "ActionType"),
        };
    }

    private static string? ReadProperty(LogEvent entry, string name)
    {
        if (!entry.Properties.TryGetValue(name, out var value))
        {
            return null;
        }

        return value switch
        {
            ScalarValue scalar => scalar.Value?.ToString(),
            _ => value.ToString(),
        };
    }

    internal static IReadOnlyList<string> StructuredPropertyKeys => StructuredPropertyNames;
}

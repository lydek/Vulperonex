using System.Collections.Concurrent;

namespace Vulperonex.Application.Workflows.Chat;

public sealed class WorkflowChatEchoTracker(TimeProvider timeProvider)
{
    private static readonly TimeSpan EchoTtl = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void Track(string platform, string message)
    {
        var key = BuildKey(platform, message);
        _entries[key] = timeProvider.GetUtcNow();
        PruneExpired(timeProvider.GetUtcNow());
    }

    public bool TryConsume(string platform, string message)
    {
        var now = timeProvider.GetUtcNow();
        var key = BuildKey(platform, message);
        if (_entries.TryGetValue(key, out var seenAt) && now - seenAt <= EchoTtl)
        {
            _entries.TryRemove(key, out _);
            return true;
        }

        PruneExpired(now);
        return false;
    }

    private void PruneExpired(DateTimeOffset now)
    {
        foreach (var entry in _entries)
        {
            if (now - entry.Value > EchoTtl)
            {
                _entries.TryRemove(entry.Key, out _);
            }
        }
    }

    private static string BuildKey(string platform, string message)
    {
        return $"{platform.Trim()}::{message.Trim()}";
    }
}

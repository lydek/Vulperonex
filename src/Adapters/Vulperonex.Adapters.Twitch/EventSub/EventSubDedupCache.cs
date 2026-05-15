namespace Vulperonex.Adapters.Twitch.EventSub;

public sealed class EventSubDedupCache(TimeProvider timeProvider, int capacity = 1000, TimeSpan? ttl = null)
{
    private readonly Dictionary<(string Platform, string SourceEventId), DateTimeOffset> _seen = [];
    private readonly TimeSpan _ttl = ttl ?? TimeSpan.FromMinutes(10);

    public bool TryMarkNew(string platform, string sourceEventId)
    {
        var now = timeProvider.GetUtcNow();
        RemoveExpired(now);

        var key = (platform, sourceEventId);
        if (_seen.ContainsKey(key))
        {
            return false;
        }

        if (_seen.Count >= capacity)
        {
            var oldest = _seen.OrderBy(pair => pair.Value).First().Key;
            _seen.Remove(oldest);
        }

        _seen[key] = now;
        return true;
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var key in _seen.Where(pair => now - pair.Value >= _ttl).Select(pair => pair.Key).ToArray())
        {
            _seen.Remove(key);
        }
    }
}

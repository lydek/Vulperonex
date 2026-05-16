namespace Vulperonex.Application.Members;

internal sealed class MemberEventDedupCache(TimeProvider timeProvider, int capacity = 1000, TimeSpan? ttl = null)
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Platform, string SourceEventId), DateTimeOffset> _seenAtByKey = [];
    private readonly Queue<(string Platform, string SourceEventId)> _insertionOrder = [];
    private readonly TimeSpan _ttl = ttl ?? TimeSpan.FromMinutes(10);

    public bool TryMarkNew(string platform, string sourceEventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEventId);

        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            RemoveExpired(now);

            var key = (platform, sourceEventId);
            if (_seenAtByKey.ContainsKey(key))
            {
                return false;
            }

            while (_seenAtByKey.Count >= capacity && _insertionOrder.TryDequeue(out var oldest))
            {
                _seenAtByKey.Remove(oldest);
            }

            _seenAtByKey[key] = now;
            _insertionOrder.Enqueue(key);
            return true;
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        while (_insertionOrder.TryPeek(out var oldest)
            && _seenAtByKey.TryGetValue(oldest, out var seenAt)
            && now - seenAt >= _ttl)
        {
            _insertionOrder.Dequeue();
            _seenAtByKey.Remove(oldest);
        }
    }
}

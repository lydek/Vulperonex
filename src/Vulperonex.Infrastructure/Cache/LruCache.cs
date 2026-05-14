namespace Vulperonex.Infrastructure.Cache;

public sealed class LruCache<TKey, TValue>
    where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _entries = [];
    private readonly LinkedList<Entry> _usage = [];

    public LruCache(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    public bool TryGet(TKey key, out TValue value)
    {
        if (!_entries.TryGetValue(key, out var node))
        {
            value = default!;
            return false;
        }

        _usage.Remove(node);
        _usage.AddFirst(node);
        value = node.Value.Value;
        return true;
    }

    public void Set(TKey key, TValue value)
    {
        if (_entries.TryGetValue(key, out var existing))
        {
            existing.Value = new Entry(key, value);
            _usage.Remove(existing);
            _usage.AddFirst(existing);
            return;
        }

        var node = new LinkedListNode<Entry>(new Entry(key, value));
        _usage.AddFirst(node);
        _entries[key] = node;

        while (_entries.Count > _capacity)
        {
            var last = _usage.Last!;
            _usage.RemoveLast();
            _entries.Remove(last.Value.Key);
        }
    }

    private sealed record Entry(TKey Key, TValue Value);
}

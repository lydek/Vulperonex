using System.Collections.Concurrent;
using Vulperonex.Application.EventTypes;

namespace Vulperonex.Infrastructure.EventTypes;

public sealed class InMemoryStreamEventTypeRegistry : IStreamEventTypeRegistry
{
    private readonly ConcurrentDictionary<string, StreamEventTypeMetadata> _metadataByKey = new(StringComparer.Ordinal);

    public void Register(StreamEventTypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (string.IsNullOrWhiteSpace(metadata.Key))
        {
            throw new ArgumentException("Event type key is required.", nameof(metadata));
        }

        _metadataByKey.TryAdd(metadata.Key, metadata);
    }

    public bool IsKnown(string key)
    {
        return _metadataByKey.ContainsKey(key);
    }

    public bool IsKnownForWorkflow(string key)
    {
        return _metadataByKey.TryGetValue(key, out var metadata) && !metadata.IsSystemEvent;
    }

    public IReadOnlyCollection<StreamEventTypeMetadata> GetAll()
    {
        return _metadataByKey.Values
            .Where(metadata => !metadata.IsSystemEvent)
            .OrderBy(metadata => metadata.Key, StringComparer.Ordinal)
            .ToArray();
    }
}

namespace Vulperonex.Infrastructure.Data.Entities;

public sealed class CounterEntity
{
    public string Key { get; set; } = string.Empty;

    public long Value { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

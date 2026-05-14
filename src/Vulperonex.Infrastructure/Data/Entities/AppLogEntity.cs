namespace Vulperonex.Infrastructure.Data.Entities;

public sealed class AppLogEntity
{
    public long Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

namespace Vulperonex.Domain.Events;

internal static class StreamEventId
{
    public static string NewUlidString() => UlidGenerator.NewUlidString();
}

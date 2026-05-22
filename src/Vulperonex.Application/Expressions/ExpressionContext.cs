namespace Vulperonex.Application.Expressions;

public sealed record ExpressionContext(
    IReadOnlyDictionary<string, object?> Trigger,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Steps,
    IReadOnlyDictionary<string, string> Args,
    IReadOnlyDictionary<string, object?> Member)
{
    public static ExpressionContext Empty { get; } = new(
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
}

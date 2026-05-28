namespace Vulperonex.Application.Expressions;

public sealed record ExpressionContext(
    IReadOnlyDictionary<string, object?> Trigger,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Steps,
    IReadOnlyDictionary<string, string> Args,
    IReadOnlyDictionary<string, object?> Member,
    IReadOnlyDictionary<string, object?> Failure)
{
    public string? RuleId { get; init; }
    public string? RuleName { get; init; }
    public ExpressionContext(
        IReadOnlyDictionary<string, object?> Trigger,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Steps,
        IReadOnlyDictionary<string, string> Args,
        IReadOnlyDictionary<string, object?> Member)
        : this(
            Trigger,
            Steps,
            Args,
            Member,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase))
    {
    }

    public static ExpressionContext Empty { get; } = new(
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
}

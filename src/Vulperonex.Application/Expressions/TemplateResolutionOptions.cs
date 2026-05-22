namespace Vulperonex.Application.Expressions;

public sealed record TemplateResolutionOptions
{
    public static TemplateResolutionOptions Default { get; } = new();

    public bool StrictMissingPlaceholders { get; init; }
}

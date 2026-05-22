namespace Vulperonex.Application.Expressions;

public sealed class TemplateMissingPlaceholderException(string placeholder)
    : InvalidOperationException($"Template placeholder '{placeholder}' could not be resolved.")
{
    public string Placeholder { get; } = placeholder;
}

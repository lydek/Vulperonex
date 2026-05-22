using System.Collections;
using System.Text.RegularExpressions;
using Vulperonex.Application.Expressions;

namespace Vulperonex.Infrastructure.Expressions;

public sealed partial class TemplateResolver : ITemplateResolver
{
    public string Resolve(
        string template,
        ExpressionContext context,
        TemplateResolutionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(context);

        var match = PlaceholderRegex().Match(template);
        if (!match.Success)
        {
            return template;
        }

        var effectiveOptions = options ?? TemplateResolutionOptions.Default;
        return PlaceholderRegex().Replace(
            template,
            placeholderMatch => ResolvePlaceholder(
                placeholderMatch.Groups["path"].Value,
                context,
                effectiveOptions));
    }

    private static string ResolvePlaceholder(
        string placeholder,
        ExpressionContext context,
        TemplateResolutionOptions options)
    {
        var parts = placeholder.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !TryResolveNamespace(parts, context, out var value))
        {
            if (options.StrictMissingPlaceholders)
            {
                throw new TemplateMissingPlaceholderException(placeholder);
            }

            return string.Empty;
        }

        return value?.ToString() ?? string.Empty;
    }

    private static bool TryResolveNamespace(
        IReadOnlyList<string> parts,
        ExpressionContext context,
        out object? value)
    {
        value = parts[0] switch
        {
            "Trigger" => context.Trigger,
            "Step" => context.Steps,
            "Args" => context.Args,
            "Member" => context.Member,
            "Failure" => context.Failure,
            _ => null,
        };

        if (value is null)
        {
            return false;
        }

        var current = value;
        for (var index = 1; index < parts.Count; index++)
        {
            if (!TryReadMember(current, parts[index], out value))
            {
                return false;
            }

            if (value is null && index < parts.Count - 1)
            {
                return false;
            }

            current = value!;
        }

        return true;
    }

    private static bool TryReadMember(object source, string memberName, out object? value)
    {
        if (source is IReadOnlyDictionary<string, object?> objectDictionary
            && objectDictionary.TryGetValue(memberName, out value))
        {
            return true;
        }

        if (source is IReadOnlyDictionary<string, string> stringDictionary
            && stringDictionary.TryGetValue(memberName, out var stringValue))
        {
            value = stringValue;
            return true;
        }

        if (source is IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> nestedDictionary
            && nestedDictionary.TryGetValue(memberName, out var nestedValue))
        {
            value = nestedValue;
            return true;
        }

        if (source is IDictionary dictionary && dictionary.Contains(memberName))
        {
            value = dictionary[memberName];
            return true;
        }

        value = null;
        return false;
    }

    [GeneratedRegex(@"\{(?<path>[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+)\}")]
    private static partial Regex PlaceholderRegex();
}

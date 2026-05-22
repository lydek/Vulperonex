using System.Text.RegularExpressions;
using NCalc;
using Vulperonex.Application.Expressions;
using WorkflowExpressionContext = Vulperonex.Application.Expressions.ExpressionContext;

namespace Vulperonex.Infrastructure.Expressions;

public sealed partial class NCalcExpressionEvaluator : IExpressionEvaluator
{
    public object? Evaluate(string expression, WorkflowExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        try
        {
            var ncalcExpression = new Expression(
                RewriteNamespacePaths(expression),
                ExpressionOptions.CaseInsensitiveStringComparer);

            foreach (var parameter in FlattenParameters(context))
            {
                ncalcExpression.Parameters[parameter.Key] = parameter.Value;
            }

            if (ncalcExpression.HasErrors())
            {
                return false;
            }

            return ncalcExpression.Evaluate();
        }
        catch
        {
            return false;
        }
    }

    private static string RewriteNamespacePaths(string expression)
    {
        return NamespacePathRegex().Replace(expression, match => $"[{match.Value}]");
    }

    private static IReadOnlyDictionary<string, object?> FlattenParameters(WorkflowExpressionContext context)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        AddNamespace(parameters, "Trigger", context.Trigger);
        AddNamespace(parameters, "Member", context.Member);
        AddNamespace(parameters, "Failure", context.Failure);

        foreach (var (key, value) in context.Args)
        {
            parameters[$"Args.{key}"] = value;
        }

        foreach (var (stepName, values) in context.Steps)
        {
            AddNamespace(parameters, $"Step.{stepName}", values);
        }

        return parameters;
    }

    private static void AddNamespace(
        IDictionary<string, object?> parameters,
        string prefix,
        IReadOnlyDictionary<string, object?> values)
    {
        foreach (var (key, value) in values)
        {
            parameters[$"{prefix}.{key}"] = value;
        }
    }

    [GeneratedRegex(@"(?<![\w\]])(?:Trigger|Member|Args|Step|Failure)\.[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)*")]
    private static partial Regex NamespacePathRegex();
}

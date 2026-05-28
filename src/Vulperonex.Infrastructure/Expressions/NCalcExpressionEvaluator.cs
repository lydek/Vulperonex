using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.RegularExpressions;
using NCalc;
using Vulperonex.Application.Expressions;
using WorkflowExpressionContext = Vulperonex.Application.Expressions.ExpressionContext;

namespace Vulperonex.Infrastructure.Expressions;

public sealed partial class NCalcExpressionEvaluator : IExpressionEvaluator
{
    private readonly ILogger<NCalcExpressionEvaluator> _logger;

    public NCalcExpressionEvaluator(ILogger<NCalcExpressionEvaluator> logger)
    {
        _logger = logger;
    }

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
                var hash = ComputeExpressionHash(expression);
                _logger.LogWarning(
                    "Expression parsing failed. RuleId={RuleId}, RuleName={RuleName}, ExpressionHash={ExpressionHash}, ErrorClass=ParseError",
                    context.RuleId ?? "N/A",
                    context.RuleName ?? "N/A",
                    hash);
                return false;
            }

            return ncalcExpression.Evaluate();
        }
        catch (Exception ex)
        {
            var hash = ComputeExpressionHash(expression);
            _logger.LogWarning(
                ex,
                "Expression evaluation failed. RuleId={RuleId}, RuleName={RuleName}, ExpressionHash={ExpressionHash}, ErrorClass=EvalError, ExceptionType={ExceptionType}",
                context.RuleId ?? "N/A",
                context.RuleName ?? "N/A",
                hash,
                ex.GetType().Name);
            return false;
        }
    }

    private static string ComputeExpressionHash(string expression)
    {
        if (string.IsNullOrEmpty(expression)) return string.Empty;
        var bytes = System.Text.Encoding.UTF8.GetBytes(expression);
        var hashBytes = System.Security.Cryptography.SHA1.HashData(bytes);
        return Convert.ToHexString(hashBytes)[..8];
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

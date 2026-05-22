namespace Vulperonex.Application.Expressions;

public interface IExpressionEvaluator
{
    object? Evaluate(string expression, ExpressionContext context);
}

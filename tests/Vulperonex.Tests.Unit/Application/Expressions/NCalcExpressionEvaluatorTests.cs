using FluentAssertions;
using Vulperonex.Application.Expressions;
using Vulperonex.Infrastructure.Expressions;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Expressions;

public sealed class NCalcExpressionEvaluatorTests
{
    [Fact]
    public void Given_BooleanExpressionWithContextScalars_When_Evaluated_Then_ReturnsTrue()
    {
        var evaluator = new NCalcExpressionEvaluator();

        var result = evaluator.Evaluate(
            "Member.IsBroadcaster == true && Trigger.Counter > 5 && Args.Target == 'bob'",
            NewContext());

        result.Should().Be(true);
    }

    [Fact]
    public void Given_ExpressionWithStepOutput_When_Evaluated_Then_ReturnsCalculatedValue()
    {
        var evaluator = new NCalcExpressionEvaluator();

        var result = evaluator.Evaluate("Step.Counter.Value + 2", NewContext());

        result.Should().Be(9);
    }

    [Fact]
    public void Given_InvalidExpression_When_Evaluated_Then_ReturnsFalse()
    {
        var evaluator = new NCalcExpressionEvaluator();

        var result = evaluator.Evaluate("this is not valid", NewContext());

        result.Should().Be(false);
    }

    private static ExpressionContext NewContext()
    {
        return new ExpressionContext(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Counter"] = 6,
            },
            new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Counter"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Value"] = 7,
                },
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Target"] = "bob",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["IsBroadcaster"] = true,
            });
    }
}

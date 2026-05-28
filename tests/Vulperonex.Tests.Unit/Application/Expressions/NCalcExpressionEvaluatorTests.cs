using FluentAssertions;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Expressions;
using Vulperonex.Infrastructure.Expressions;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Expressions;

public sealed class NCalcExpressionEvaluatorTests
{
    [Fact]
    public void Given_BooleanExpressionWithContextScalars_When_Evaluated_Then_ReturnsTrue()
    {
        var evaluator = new NCalcExpressionEvaluator(Microsoft.Extensions.Logging.Abstractions.NullLogger<NCalcExpressionEvaluator>.Instance);

        var result = evaluator.Evaluate(
            "Member.IsBroadcaster == true && Trigger.Counter > 5 && Args.Target == 'bob'",
            NewContext());

        result.Should().Be(true);
    }

    [Fact]
    public void Given_ExpressionWithStepOutput_When_Evaluated_Then_ReturnsCalculatedValue()
    {
        var evaluator = new NCalcExpressionEvaluator(Microsoft.Extensions.Logging.Abstractions.NullLogger<NCalcExpressionEvaluator>.Instance);

        var result = evaluator.Evaluate("Step.Counter.Value + 2", NewContext());

        result.Should().Be(9);
    }

    [Fact]
    public void Given_InvalidExpression_When_Evaluated_Then_ReturnsFalse()
    {
        var evaluator = new NCalcExpressionEvaluator(Microsoft.Extensions.Logging.Abstractions.NullLogger<NCalcExpressionEvaluator>.Instance);

        var result = evaluator.Evaluate("this is not valid", NewContext());

        result.Should().Be(false);
    }

    [Fact]
    public void Given_InvalidExpression_When_Evaluate_Then_LogsWarningWithoutBody()
    {
        var logger = new CollectingLogger();
        var evaluator = new NCalcExpressionEvaluator(logger);
        var context = NewContext() with { RuleId = "r1", RuleName = "Test Rule" };

        var result = evaluator.Evaluate("this is not valid", context);

        result.Should().Be(false);
        logger.LoggedMessages.Should().ContainSingle();
        var log = logger.LoggedMessages[0];
        log.Should().Contain("r1");
        log.Should().Contain("Test Rule");
        log.Should().Contain("ExpressionHash=");
        log.Should().Contain("ErrorClass=ParseError");
        log.Should().NotContain("this is not valid");
    }

    [Fact]
    public void Given_ExpressionThrowingAtRuntime_When_Evaluate_Then_LogsWarningWithoutBody()
    {
        var logger = new CollectingLogger();
        var evaluator = new NCalcExpressionEvaluator(logger);
        var context = NewContext() with { RuleId = "r1", RuleName = "Test Rule" };

        // Calling a non-existent method will throw a runtime evaluation error in NCalc
        var result = evaluator.Evaluate("NonExistentMethod(5)", context);

        result.Should().Be(false);
        logger.LoggedMessages.Should().ContainSingle();
        var log = logger.LoggedMessages[0];
        log.Should().Contain("r1");
        log.Should().Contain("Test Rule");
        log.Should().Contain("ExpressionHash=");
        log.Should().Contain("ErrorClass=EvalError");
        log.Should().NotContain("1 / 0");
    }

    private sealed class CollectingLogger : ILogger<NCalcExpressionEvaluator>
    {
        public List<string> LoggedMessages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LoggedMessages.Add(formatter(state, exception));
        }
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

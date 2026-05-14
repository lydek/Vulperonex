using FluentAssertions;
using Vulperonex.Application.Time;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows.Conditions;

public sealed class WorkflowConditionEvaluatorTests
{
    [Theory]
    [InlineData(UserRoleMatchMode.HasAny, StreamRole.Subscriber | StreamRole.Vip, StreamRole.Vip, true)]
    [InlineData(UserRoleMatchMode.HasAll, StreamRole.Subscriber | StreamRole.Vip, StreamRole.Subscriber, false)]
    [InlineData(UserRoleMatchMode.NotHave, StreamRole.Moderator, StreamRole.Subscriber, true)]
    public void Given_UserRoleCondition_When_Evaluated_Then_RoleModeIsApplied(
        UserRoleMatchMode mode,
        StreamRole requiredRoles,
        StreamRole userRoles,
        bool expected)
    {
        var evaluator = new WorkflowConditionEvaluator(new FakeClock());
        var condition = new UserRoleCondition { Mode = mode, Roles = requiredRoles };
        var streamEvent = NewMessageEvent("alice", roles: userRoles);

        var result = evaluator.IsMatch(condition, new ConditionEvaluationContext(streamEvent, "rule-1"));

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(MessageContentMatchMode.PrefixMatch, "!hello", "!hello world", true)]
    [InlineData(MessageContentMatchMode.ContainsMatch, "hello", "well hello there", true)]
    [InlineData(MessageContentMatchMode.FullRegex, "^!h\\w+$", "!hello", true)]
    [InlineData(MessageContentMatchMode.FullRegex, "^!h\\w+$", "hello", false)]
    public void Given_MessageContentCondition_When_Evaluated_Then_MessageTextIsMatched(
        MessageContentMatchMode mode,
        string pattern,
        string message,
        bool expected)
    {
        var evaluator = new WorkflowConditionEvaluator(new FakeClock());
        var condition = new MessageContentCondition { MatchMode = mode, Pattern = pattern };
        var streamEvent = NewMessageEvent("alice", message);

        var result = evaluator.IsMatch(condition, new ConditionEvaluationContext(streamEvent, "rule-1"));

        result.Should().Be(expected);
    }

    [Fact]
    public void Given_InvalidRegexCondition_When_Validated_Then_InvalidRegexPatternIsReturned()
    {
        var validator = new WorkflowConditionValidator();
        var condition = new MessageContentCondition
        {
            MatchMode = MessageContentMatchMode.FullRegex,
            Pattern = "[",
        };

        validator.Validate(condition).Should().Be(new ConditionValidationResult(false, "INVALID_REGEX_PATTERN"));
    }

    [Fact]
    public void Given_TooLongRegexCondition_When_Validated_Then_InvalidRegexPatternIsReturned()
    {
        var validator = new WorkflowConditionValidator();
        var condition = new MessageContentCondition
        {
            MatchMode = MessageContentMatchMode.FullRegex,
            Pattern = new string('a', WorkflowConditionValidator.MaxRegexPatternLength + 1),
        };

        validator.Validate(condition).Should().Be(new ConditionValidationResult(false, "INVALID_REGEX_PATTERN"));
    }

    [Fact]
    public void Given_CooldownCondition_When_EvaluatedWithFakeClock_Then_ItBlocksUntilDurationPasses()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero));
        var evaluator = new WorkflowConditionEvaluator(clock);
        var condition = new CooldownCondition { DurationSeconds = 60 };
        var context = new ConditionEvaluationContext(NewMessageEvent("alice"), "rule-1");

        evaluator.IsMatch(condition, context).Should().BeTrue();
        evaluator.IsMatch(condition, context).Should().BeFalse();

        clock.UtcNow = clock.UtcNow.AddSeconds(60);

        evaluator.IsMatch(condition, context).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(86_401)]
    public void Given_CooldownDurationOutOfRange_When_Validated_Then_InvalidActionConfigIsReturned(int durationSeconds)
    {
        var validator = new WorkflowConditionValidator();

        validator.Validate(new CooldownCondition { DurationSeconds = durationSeconds })
            .Should().Be(new ConditionValidationResult(false, "INVALID_ACTION_CONFIG"));
    }

    [Fact]
    public void Given_UnknownConditionType_When_Evaluated_Then_ItFailsClosed()
    {
        var evaluator = new WorkflowConditionEvaluator(new FakeClock());

        evaluator.IsMatch(new UnknownCondition(), new ConditionEvaluationContext(NewMessageEvent("alice"), "rule-1"))
            .Should().BeFalse();
    }

    private static UserSentMessageEvent NewMessageEvent(
        string userId,
        string message = "!hello",
        StreamRole roles = StreamRole.None)
    {
        return new UserSentMessageEvent
        {
            Platform = "twitch",
            User = new StreamUser("twitch", userId, userId, roles),
            MessageText = message,
        };
    }

    private sealed record UnknownCondition : WorkflowCondition
    {
        public override string Type => "unknown";
    }

    private sealed class FakeClock(DateTimeOffset? utcNow = null) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } =
            utcNow ?? new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
    }
}

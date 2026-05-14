using FluentAssertions;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows.Actions;

public sealed class SendChatMessageActionExecutorTests
{
    [Fact]
    public async Task Given_SendChatMessageActionWithoutTargetPlatform_When_Executed_Then_SourcePlatformSenderIsUsed()
    {
        var twitch = new RecordingChatSender("twitch");
        var youtube = new RecordingChatSender("youtube");
        var executor = new SendChatMessageActionExecutor([twitch, youtube], new TemplateRenderer());

        await executor.ExecuteAsync(
            new SendChatMessageAction { Template = "Hello {user.displayName}" },
            NewContext("twitch"),
            TestContext.Current.CancellationToken);

        twitch.Messages.Should().ContainSingle().Which.Should().Be("Hello Alice");
        youtube.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_SendChatMessageActionWithTargetPlatform_When_Executed_Then_TargetPlatformSenderIsUsed()
    {
        var twitch = new RecordingChatSender("twitch");
        var youtube = new RecordingChatSender("youtube");
        var executor = new SendChatMessageActionExecutor([twitch, youtube], new TemplateRenderer());

        await executor.ExecuteAsync(
            new SendChatMessageAction
            {
                Template = "Hello {user.displayName}",
                TargetPlatform = "youtube",
            },
            NewContext("twitch"),
            TestContext.Current.CancellationToken);

        twitch.Messages.Should().BeEmpty();
        youtube.Messages.Should().ContainSingle().Which.Should().Be("Hello Alice");
    }

    [Fact]
    public async Task Given_NoMatchingSender_When_Executed_Then_ActionIsSkipped()
    {
        var sender = new RecordingChatSender("youtube");
        var executor = new SendChatMessageActionExecutor([sender], new TemplateRenderer());

        await executor.ExecuteAsync(
            new SendChatMessageAction { Template = "Hello" },
            NewContext("twitch"),
            TestContext.Current.CancellationToken);

        sender.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Given_TemplateContainsKnownUnknownAndNullPlaceholders_When_Rendered_Then_KnownValuesAreExpandedSafely()
    {
        var renderer = new TemplateRenderer();
        var streamEvent = new UserSentMessageEvent
        {
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
            MessageText = "hi",
        };

        var rendered = renderer.Render(
            "{user.displayName}:{event.message}:{event.unknown}:{user.missing}",
            streamEvent);

        rendered.Should().Be("Alice:hi:{event.unknown}:{user.missing}");
    }

    private static ActionExecutionContext NewContext(string platform)
    {
        var streamEvent = new UserSentMessageEvent
        {
            Platform = platform,
            User = new StreamUser(platform, "alice", "Alice"),
            MessageText = "!hello",
        };
        var rule = new WorkflowRule
        {
            Id = "rule-1",
            Name = "hello",
            EventTypeKey = StreamEventKeys.UserSentMessage,
        };

        return new ActionExecutionContext(streamEvent, rule, ActionIndex: 0);
    }

    private sealed class RecordingChatSender(string platform) : IPlatformChatSender
    {
        public string Platform { get; } = platform;
        public List<string> Messages { get; } = [];

        public Task SendAsync(string message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}

using FluentAssertions;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Chat;
using Vulperonex.Infrastructure.Expressions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows.Actions;

public sealed class SendChatMessageActionExecutorTests
{
    [Fact]
    public async Task Given_SendChatMessageActionWithoutTargetPlatform_When_Executed_Then_SourcePlatformMessageIsQueued()
    {
        var outbox = new InMemoryChatOutbox(TimeProvider.System);
        var executor = new SendChatMessageActionExecutor(outbox, new TemplateResolver(), new TemplateRenderer());
        var context = NewContext("twitch");

        await executor.ExecuteAsync(
            new SendChatMessageAction { Template = "Hello {user.displayName}" },
            context,
            TestContext.Current.CancellationToken);

        var item = (await outbox.SnapshotAsync(TestContext.Current.CancellationToken))
            .Should().ContainSingle().Subject;
        item.Platform.Should().Be("twitch");
        item.Message.Should().Be("Hello Alice");
        item.Status.Should().Be(ChatOutboxItemStatus.Pending);
        item.DedupKey.Should().Be($"action:{context.StreamEvent.EventId}:rule-1:0");
    }

    [Fact]
    public async Task Given_SendChatMessageActionWithTargetPlatformChannelAndDedupKey_When_Executed_Then_MessageIsQueued()
    {
        var outbox = new InMemoryChatOutbox(TimeProvider.System);
        var executor = new SendChatMessageActionExecutor(outbox, new TemplateResolver(), new TemplateRenderer());

        await executor.ExecuteAsync(
            new SendChatMessageAction
            {
                Template = "Hello {user.displayName}",
                TargetPlatform = "youtube",
                Channel = "{user.id}",
                DedupKey = "hello:{user.id}",
            },
            NewContext("twitch"),
            TestContext.Current.CancellationToken);

        var item = (await outbox.SnapshotAsync(TestContext.Current.CancellationToken))
            .Should().ContainSingle().Subject;
        item.Platform.Should().Be("youtube");
        item.Channel.Should().Be("alice");
        item.Message.Should().Be("Hello Alice");
        item.DedupKey.Should().Be("hello:alice");
    }

    [Fact]
    public async Task Given_NoMatchingSender_When_Executed_Then_ActionIsSkipped()
    {
        var sender = new RecordingChatSender("youtube");
        var executor = new SendChatMessageActionExecutor([sender], new TemplateResolver(), new TemplateRenderer());

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

    [Fact]
    public async Task Given_ExpressionContextPlaceholders_When_Executed_Then_StepAndMemberValuesAreRendered()
    {
        var outbox = new InMemoryChatOutbox(TimeProvider.System);
        var executor = new SendChatMessageActionExecutor(outbox, new TemplateResolver(), new TemplateRenderer());
        var context = NewContext(
            "twitch",
            new Vulperonex.Application.Expressions.ExpressionContext(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["UserDisplayName"] = "Alice",
                },
                new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CheckIn"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["CheckInCount"] = 4,
                        ["TotalLoyalty"] = 28,
                    },
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DisplayName"] = "Alice",
                }));

        await executor.ExecuteAsync(
            new SendChatMessageAction
            {
                Template = "@{Member.DisplayName} checked in {Step.CheckIn.CheckInCount} / {CheckIn.TotalLoyalty}",
            },
            context,
            TestContext.Current.CancellationToken);

        var item = (await outbox.SnapshotAsync(TestContext.Current.CancellationToken))
            .Should().ContainSingle().Subject;
        item.Message.Should().Be("@Alice checked in 4 / 28");
    }

    [Fact]
    public async Task Given_OutputDestinationIncludesOverlay_When_Executed_Then_AssistantOverlayMessageIsPublishedImmediately()
    {
        var outbox = new InMemoryChatOutbox(TimeProvider.System);
        var overlaySink = new RecordingOverlaySink();
        var executor = new SendChatMessageActionExecutor(
            outbox,
            new TemplateResolver(),
            new TemplateRenderer(),
            overlaySink,
            new FakeSettingsService(WorkflowChatOutputDestination.Dual));

        await executor.ExecuteAsync(
            new SendChatMessageAction { Template = "Hello now" },
            NewContext("twitch"),
            TestContext.Current.CancellationToken);

        overlaySink.Messages.Should().ContainSingle().Which.Should().Be("Hello now");
    }

    [Fact]
    public async Task Given_OutputDestinationExcludesOverlay_When_Executed_Then_AssistantOverlayMessageIsNotPublished()
    {
        var outbox = new InMemoryChatOutbox(TimeProvider.System);
        var overlaySink = new RecordingOverlaySink();
        var executor = new SendChatMessageActionExecutor(
            outbox,
            new TemplateResolver(),
            new TemplateRenderer(),
            overlaySink,
            new FakeSettingsService(WorkflowChatOutputDestination.PlatformOnly));

        await executor.ExecuteAsync(
            new SendChatMessageAction { Template = "Hello platform" },
            NewContext("twitch"),
            TestContext.Current.CancellationToken);

        overlaySink.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_OutboxDispatcher_When_Executed_Then_MessageIsFlushedBeforeActionCompletes()
    {
        var outbox = new InMemoryChatOutbox(TimeProvider.System);
        var dispatcher = new RecordingOutboxDispatcher();
        var executor = new SendChatMessageActionExecutor(
            outbox,
            new TemplateResolver(),
            new TemplateRenderer(),
            chatOutboxDispatcher: dispatcher);

        await executor.ExecuteAsync(
            new SendChatMessageAction { Template = "Hello ordered" },
            NewContext("twitch"),
            TestContext.Current.CancellationToken);

        dispatcher.DispatchedItems.Should().ContainSingle();
    }

    private static ActionExecutionContext NewContext(
        string platform,
        Vulperonex.Application.Expressions.ExpressionContext? expressionContext = null)
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

        return new ActionExecutionContext(streamEvent, rule, ActionIndex: 0, ExpressionContext: expressionContext);
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

    private sealed class RecordingOverlaySink : IWorkflowChatOverlaySink
    {
        public List<string> Messages { get; } = [];
        public List<WorkflowCheckInCardOverlayMessage> CheckInCards { get; } = [];

        public Task PublishAssistantMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task PublishCheckInCardAsync(
            WorkflowCheckInCardOverlayMessage message,
            CancellationToken cancellationToken = default)
        {
            CheckInCards.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxDispatcher : IChatOutboxDispatcher
    {
        public List<Guid> DispatchedItems { get; } = [];

        public Task<int> DispatchOnceAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DispatchItemAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DispatchedItems.Add(id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsService(string outputDestination) : ISystemSettingsService
    {
        public IObservable<SettingChangedEvent> Changes { get; } = new NoopObservable();

        public Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
        {
            if (key == SystemSettingKey.WorkflowChatOutputDestination && typeof(T) == typeof(string))
            {
                return Task.FromResult((T)(object)outputDestination);
            }

            return Task.FromResult(defaultValue);
        }

        public Task SetAsync<T>(string key, T value, string category, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoopObservable : IObservable<SettingChangedEvent>
    {
        public IDisposable Subscribe(IObserver<SettingChangedEvent> observer)
        {
            return new NoopDisposable();
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

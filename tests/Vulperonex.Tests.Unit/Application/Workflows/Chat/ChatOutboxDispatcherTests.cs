using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Chat;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows.Chat;

public sealed class ChatOutboxDispatcherTests
{
    [Fact]
    public async Task Given_BurstMessages_When_DispatchedOnce_Then_SystemSettingRateLimitIsApplied()
    {
        var outbox = new InMemoryChatOutbox(TimeProvider.System);
        var sender = new RecordingChatSender("twitch");
        var dispatcher = NewDispatcher(outbox, [sender], perSecond: 5);
        for (var i = 0; i < 100; i++)
        {
            await outbox.EnqueueAsync(
                "twitch",
                channel: null,
                $"message-{i}",
                cancellationToken: TestContext.Current.CancellationToken);
        }

        var dispatched = await dispatcher.DispatchOnceAsync(TestContext.Current.CancellationToken);

        dispatched.Should().Be(5);
        sender.Messages.Should().Equal("message-0", "message-1", "message-2", "message-3", "message-4");
        var items = await outbox.SnapshotAsync(TestContext.Current.CancellationToken);
        items.Count(item => item.Status == ChatOutboxItemStatus.Sent).Should().Be(5);
        items.Count(item => item.Status == ChatOutboxItemStatus.Pending).Should().Be(95);
    }

    [Fact]
    public async Task Given_NoMatchingSender_When_Dispatched_Then_ItemIsSkipped()
    {
        var outbox = new InMemoryChatOutbox(TimeProvider.System);
        var dispatcher = NewDispatcher(outbox, [], perSecond: 5);
        await outbox.EnqueueAsync(
            "twitch",
            channel: null,
            "hello",
            cancellationToken: TestContext.Current.CancellationToken);

        await dispatcher.DispatchOnceAsync(TestContext.Current.CancellationToken);

        var item = (await outbox.SnapshotAsync(TestContext.Current.CancellationToken))
            .Should().ContainSingle().Subject;
        item.Status.Should().Be(ChatOutboxItemStatus.Skipped);
        item.ErrorMessage.Should().Contain("No chat sender registered");
    }

    [Fact]
    public async Task Given_SenderFails_When_Dispatched_Then_ItemIsMarkedFailed()
    {
        var outbox = new InMemoryChatOutbox(TimeProvider.System);
        var dispatcher = NewDispatcher(outbox, [new FailingChatSender("twitch")], perSecond: 5);
        await outbox.EnqueueAsync(
            "twitch",
            channel: null,
            "hello",
            cancellationToken: TestContext.Current.CancellationToken);

        await dispatcher.DispatchOnceAsync(TestContext.Current.CancellationToken);

        var item = (await outbox.SnapshotAsync(TestContext.Current.CancellationToken))
            .Should().ContainSingle().Subject;
        item.Status.Should().Be(ChatOutboxItemStatus.Failed);
        item.ErrorMessage.Should().Be("send failed");
    }

    private static ChatOutboxDispatcher NewDispatcher(
        IChatOutbox outbox,
        IEnumerable<IPlatformChatSender> senders,
        int perSecond)
    {
        var provider = new ServiceCollection()
            .AddSingleton<ISystemSettingsService>(new FakeSettingsService(perSecond))
            .BuildServiceProvider();

        return new ChatOutboxDispatcher(
            outbox,
            senders,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ChatOutboxDispatcher>.Instance,
            new NoopObservable());
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

    private sealed class FailingChatSender(string platform) : IPlatformChatSender
    {
        public string Platform { get; } = platform;

        public Task SendAsync(string message, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("send failed");
        }
    }

    private sealed class FakeSettingsService(int perSecond) : ISystemSettingsService
    {
        public IObservable<SettingChangedEvent> Changes { get; } = new NoopObservable();

        public Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
        {
            if (key == SystemSettingKey.ChatOutboxPerSecond && typeof(T) == typeof(int))
            {
                return Task.FromResult((T)(object)perSecond);
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

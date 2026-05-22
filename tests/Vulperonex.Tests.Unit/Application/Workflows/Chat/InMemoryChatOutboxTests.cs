using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Workflows.Chat;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows.Chat;

public sealed class InMemoryChatOutboxTests
{
    [Fact]
    public async Task Given_SameDedupKeyWithinDefaultTtl_When_Enqueued_Then_DuplicateIsDropped()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero));
        var outbox = new InMemoryChatOutbox(timeProvider);

        var first = await outbox.EnqueueAsync(
            "twitch",
            channel: null,
            "first",
            dedupKey: "same-key",
            cancellationToken: TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromHours(23));
        var second = await outbox.EnqueueAsync(
            "twitch",
            channel: null,
            "second",
            dedupKey: "same-key",
            cancellationToken: TestContext.Current.CancellationToken);

        first.IsDuplicate.Should().BeFalse();
        second.IsDuplicate.Should().BeTrue();
        var item = (await outbox.SnapshotAsync(TestContext.Current.CancellationToken))
            .Should().ContainSingle().Subject;
        item.Message.Should().Be("first");
    }

    [Fact]
    public async Task Given_SameDedupKeyAfterDefaultTtl_When_Enqueued_Then_MessageIsAccepted()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero));
        var outbox = new InMemoryChatOutbox(timeProvider);
        await outbox.EnqueueAsync(
            "twitch",
            channel: null,
            "first",
            dedupKey: "same-key",
            cancellationToken: TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromHours(24));

        var second = await outbox.EnqueueAsync(
            "twitch",
            channel: null,
            "second",
            dedupKey: "same-key",
            cancellationToken: TestContext.Current.CancellationToken);

        second.IsDuplicate.Should().BeFalse();
        var items = await outbox.SnapshotAsync(TestContext.Current.CancellationToken);
        items.Select(item => item.Message).Should().Equal("first", "second");
    }

    [Fact]
    public async Task Given_DedupTtlSetting_When_Elapsed_Then_ConfiguredTtlIsApplied()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero));
        var provider = new ServiceCollection()
            .AddSingleton<ISystemSettingsService>(new FakeSettingsService(dedupTtlHours: 1))
            .BuildServiceProvider();
        var outbox = new InMemoryChatOutbox(timeProvider, provider.GetRequiredService<IServiceScopeFactory>());
        await outbox.EnqueueAsync(
            "twitch",
            channel: null,
            "first",
            dedupKey: "same-key",
            cancellationToken: TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromHours(2));

        var second = await outbox.EnqueueAsync(
            "twitch",
            channel: null,
            "second",
            dedupKey: "same-key",
            cancellationToken: TestContext.Current.CancellationToken);

        second.IsDuplicate.Should().BeFalse();
        var items = await outbox.SnapshotAsync(TestContext.Current.CancellationToken);
        items.Select(item => item.Message).Should().Equal("first", "second");
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan value)
        {
            _utcNow += value;
        }
    }

    private sealed class FakeSettingsService(int dedupTtlHours) : ISystemSettingsService
    {
        public IObservable<SettingChangedEvent> Changes { get; } = new NoopObservable();

        public Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
        {
            if (key == SystemSettingKey.ChatOutboxDedupTtlHours && typeof(T) == typeof(int))
            {
                return Task.FromResult((T)(object)dedupTtlHours);
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

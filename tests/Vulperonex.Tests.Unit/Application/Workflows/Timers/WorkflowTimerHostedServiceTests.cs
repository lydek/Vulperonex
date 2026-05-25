using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vulperonex.Application.Modules;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Timers;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows.Timers;

public sealed class WorkflowTimerHostedServiceTests
{
    [Fact]
    public async Task Given_DueTimer_When_Ticked_Then_RuleIsInvokedAndNextFireAtAdvances()
    {
        var now = new DateTimeOffset(2026, 5, 23, 0, 1, 0, TimeSpan.Zero);
        var repository = new FakeWorkflowTimerRepository(
            new WorkflowTimer
            {
                Id = "timer-1",
                RuleId = "rule-1",
                IntervalSeconds = 30,
                IsEnabled = true,
                NextFireAt = now.AddSeconds(-30),
            });
        var invoker = new RecordingWorkflowRuleInvoker();
        var service = NewService(repository, invoker);

        var fired = await service.TickAsync(now, TestContext.Current.CancellationToken);

        fired.Should().Be(1);
        invoker.Invocations.Should().ContainSingle();
        invoker.Invocations[0].RuleId.Should().Be("rule-1");
        invoker.Invocations[0].InvocationId.Should().Be($"timer:timer-1:{now.AddSeconds(-30).ToUnixTimeMilliseconds()}");
        invoker.Invocations[0].StreamEvent.EventTypeKey.Should().Be(StreamEventKeys.WorkflowTimer);
        repository.Timers.Single().NextFireAt.Should().Be(now);
    }

    [Fact]
    public async Task Given_TimerIntervalThirtySeconds_When_TickedAcrossSixtySeconds_Then_FiresTwice()
    {
        var start = new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero);
        var repository = new FakeWorkflowTimerRepository(
            new WorkflowTimer
            {
                Id = "timer-1",
                RuleId = "rule-1",
                IntervalSeconds = 30,
                IsEnabled = true,
                NextFireAt = start.AddSeconds(30),
            });
        var invoker = new RecordingWorkflowRuleInvoker();
        var service = NewService(repository, invoker);

        await service.TickAsync(start, TestContext.Current.CancellationToken);
        await service.TickAsync(start.AddSeconds(30), TestContext.Current.CancellationToken);
        await service.TickAsync(start.AddSeconds(60), TestContext.Current.CancellationToken);

        invoker.Invocations.Select(invocation => invocation.InvocationId)
            .Should().Equal(
                $"timer:timer-1:{start.AddSeconds(30).ToUnixTimeMilliseconds()}",
                $"timer:timer-1:{start.AddSeconds(60).ToUnixTimeMilliseconds()}");
        repository.Timers.Single().NextFireAt.Should().Be(start.AddSeconds(90));
    }

    [Fact]
    public async Task Given_DisabledTimer_When_Ticked_Then_TimerDoesNotFireOrAdvance()
    {
        var now = new DateTimeOffset(2026, 5, 23, 0, 1, 0, TimeSpan.Zero);
        var repository = new FakeWorkflowTimerRepository(
            new WorkflowTimer
            {
                Id = "timer-1",
                RuleId = "rule-1",
                IntervalSeconds = 30,
                IsEnabled = false,
                NextFireAt = now.AddSeconds(-30),
            });
        var invoker = new RecordingWorkflowRuleInvoker();
        var service = NewService(repository, invoker);

        var fired = await service.TickAsync(now, TestContext.Current.CancellationToken);

        fired.Should().Be(0);
        invoker.Invocations.Should().BeEmpty();
        repository.Timers.Single().NextFireAt.Should().Be(now.AddSeconds(-30));
    }

    [Fact]
    public async Task Given_WorkflowModuleDisabled_When_Ticked_Then_NoTimerWorkRuns()
    {
        var now = new DateTimeOffset(2026, 5, 23, 0, 1, 0, TimeSpan.Zero);
        var repository = new FakeWorkflowTimerRepository(
            new WorkflowTimer
            {
                Id = "timer-1",
                RuleId = "rule-1",
                IntervalSeconds = 30,
                IsEnabled = true,
                NextFireAt = now.AddSeconds(-30),
            });
        var invoker = new RecordingWorkflowRuleInvoker();
        var provider = new ServiceCollection()
            .AddSingleton<IWorkflowTimerRepository>(repository)
            .AddSingleton<IWorkflowRuleInvoker>(invoker)
            .BuildServiceProvider();
        var service = new WorkflowTimerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new DisabledModuleStateService(),
            TimeProvider.System,
            NullLogger<WorkflowTimerHostedService>.Instance);

        var fired = await service.TickAsync(now, TestContext.Current.CancellationToken);

        fired.Should().Be(0);
        invoker.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ScopedAsyncDisposableInvoker_When_Ticked_Then_ScopeDisposesAsynchronously()
    {
        var now = new DateTimeOffset(2026, 5, 23, 0, 1, 0, TimeSpan.Zero);
        var repository = new FakeWorkflowTimerRepository(
            new WorkflowTimer
            {
                Id = "timer-1",
                RuleId = "rule-1",
                IntervalSeconds = 30,
                IsEnabled = true,
                NextFireAt = now.AddSeconds(-30),
            });
        var provider = new ServiceCollection()
            .AddSingleton<IWorkflowTimerRepository>(repository)
            .AddScoped<IWorkflowRuleInvoker, AsyncDisposableWorkflowRuleInvoker>()
            .BuildServiceProvider();
        var service = new WorkflowTimerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new AlwaysEnabledModuleStateService(),
            TimeProvider.System,
            NullLogger<WorkflowTimerHostedService>.Instance);

        var act = () => service.TickAsync(now, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    private static WorkflowTimerHostedService NewService(
        IWorkflowTimerRepository repository,
        IWorkflowRuleInvoker invoker)
    {
        var provider = new ServiceCollection()
            .AddSingleton(repository)
            .AddSingleton(invoker)
            .BuildServiceProvider();

        return new WorkflowTimerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new AlwaysEnabledModuleStateService(),
            TimeProvider.System,
            NullLogger<WorkflowTimerHostedService>.Instance);
    }

    private sealed class FakeWorkflowTimerRepository(params WorkflowTimer[] timers) : IWorkflowTimerRepository
    {
        public List<WorkflowTimer> Timers { get; } = timers.ToList();

        public Task<IReadOnlyList<WorkflowTimer>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkflowTimer>>(Timers);
        }

        public Task<WorkflowTimer?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Timers.FirstOrDefault(timer => timer.Id == id));
        }

        public Task AddAsync(WorkflowTimer timer, CancellationToken cancellationToken = default)
        {
            Timers.Add(timer);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WorkflowTimer timer, CancellationToken cancellationToken = default)
        {
            var index = Timers.FindIndex(existing => existing.Id == timer.Id);
            Timers[index] = timer;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            Timers.RemoveAll(timer => timer.Id == id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WorkflowTimer>> ListDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkflowTimer>>(
                Timers
                    .Where(timer => timer.IsEnabled && timer.NextFireAt <= now)
                    .OrderBy(timer => timer.NextFireAt)
                    .ThenBy(timer => timer.Id)
                    .ToArray());
        }

        public Task MarkFiredAsync(string id, DateTimeOffset nextFireAt, CancellationToken cancellationToken = default)
        {
            var index = Timers.FindIndex(timer => timer.Id == id);
            Timers[index] = Timers[index] with { NextFireAt = nextFireAt };
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingWorkflowRuleInvoker : IWorkflowRuleInvoker
    {
        public List<Invocation> Invocations { get; } = [];

        public Task InvokeAsync(
            string workflowRuleId,
            IStreamEvent streamEvent,
            string invocationId,
            IReadOnlyDictionary<string, string>? args = null,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(new Invocation(workflowRuleId, streamEvent, invocationId, args));
            return Task.CompletedTask;
        }
    }

    private sealed class AsyncDisposableWorkflowRuleInvoker : IWorkflowRuleInvoker, IAsyncDisposable
    {
        public Task InvokeAsync(
            string workflowRuleId,
            IStreamEvent streamEvent,
            string invocationId,
            IReadOnlyDictionary<string, string>? args = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed record Invocation(
        string RuleId,
        IStreamEvent StreamEvent,
        string InvocationId,
        IReadOnlyDictionary<string, string>? Args);

    private sealed class AlwaysEnabledModuleStateService : IModuleStateService
    {
        public Task<IReadOnlyList<ModuleStateSnapshot>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModuleStateSnapshot>>([]);

        public Task<bool> IsEnabledAsync(string moduleName, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<ModuleToggleResult> ToggleAsync(string moduleName, bool enabled, string actorKind, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class DisabledModuleStateService : IModuleStateService
    {
        public Task<IReadOnlyList<ModuleStateSnapshot>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModuleStateSnapshot>>([]);

        public Task<bool> IsEnabledAsync(string moduleName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<ModuleToggleResult> ToggleAsync(string moduleName, bool enabled, string actorKind, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

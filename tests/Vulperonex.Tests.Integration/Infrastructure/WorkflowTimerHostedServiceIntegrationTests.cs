using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vulperonex.Application.Modules;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Timers;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Workflows;
using Xunit;

namespace Vulperonex.Tests.Integration.Infrastructure;

public sealed class WorkflowTimerHostedServiceIntegrationTests
{
    [Fact]
    public async Task Given_ThirtySecondTimer_When_TickedAcrossSixtySeconds_Then_FiresTwiceAndPersistsNextFireAt()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var start = new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero);
        var repository = new WorkflowTimerRepository(context);
        await repository.AddAsync(
            new WorkflowTimer
            {
                Id = "timer-1",
                RuleId = "rule-1",
                IntervalSeconds = 30,
                IsEnabled = true,
                NextFireAt = start.AddSeconds(30),
            },
            TestContext.Current.CancellationToken);
        var invoker = new RecordingWorkflowRuleInvoker();
        var service = NewService((SqliteConnection)context.Database.GetDbConnection(), invoker);

        await service.TickAsync(start, TestContext.Current.CancellationToken);
        await service.TickAsync(start.AddSeconds(30), TestContext.Current.CancellationToken);
        await service.TickAsync(start.AddSeconds(60), TestContext.Current.CancellationToken);

        invoker.Invocations.Select(invocation => invocation.InvocationId)
            .Should().Equal(
                $"timer:timer-1:{start.AddSeconds(30).ToUnixTimeMilliseconds()}",
                $"timer:timer-1:{start.AddSeconds(60).ToUnixTimeMilliseconds()}");
        var timer = await repository.GetAsync("timer-1", TestContext.Current.CancellationToken);
        timer!.NextFireAt.Should().Be(start.AddSeconds(90));
    }

    [Fact]
    public async Task Given_DisabledTimer_When_Ticked_Then_NextFireAtDoesNotAdvance()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var now = new DateTimeOffset(2026, 5, 23, 0, 1, 0, TimeSpan.Zero);
        var repository = new WorkflowTimerRepository(context);
        await repository.AddAsync(
            new WorkflowTimer
            {
                Id = "timer-1",
                RuleId = "rule-1",
                IntervalSeconds = 30,
                IsEnabled = false,
                NextFireAt = now.AddSeconds(-30),
            },
            TestContext.Current.CancellationToken);
        var invoker = new RecordingWorkflowRuleInvoker();
        var service = NewService((SqliteConnection)context.Database.GetDbConnection(), invoker);

        await service.TickAsync(now, TestContext.Current.CancellationToken);

        invoker.Invocations.Should().BeEmpty();
        var timer = await repository.GetAsync("timer-1", TestContext.Current.CancellationToken);
        timer!.NextFireAt.Should().Be(now.AddSeconds(-30));
    }

    [Fact]
    public async Task Given_ServiceRestart_When_SameFireSlotIsTickedAgain_Then_TimerDoesNotFireTwice()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var now = new DateTimeOffset(2026, 5, 23, 0, 1, 0, TimeSpan.Zero);
        var repository = new WorkflowTimerRepository(context);
        await repository.AddAsync(
            new WorkflowTimer
            {
                Id = "timer-1",
                RuleId = "rule-1",
                IntervalSeconds = 30,
                IsEnabled = true,
                NextFireAt = now,
            },
            TestContext.Current.CancellationToken);
        var invoker = new RecordingWorkflowRuleInvoker();

        var connection = (SqliteConnection)context.Database.GetDbConnection();
        await NewService(connection, invoker).TickAsync(now, TestContext.Current.CancellationToken);
        await NewService(connection, invoker).TickAsync(now, TestContext.Current.CancellationToken);

        invoker.Invocations.Should().ContainSingle();
        var timer = await repository.GetAsync("timer-1", TestContext.Current.CancellationToken);
        timer!.NextFireAt.Should().Be(now.AddSeconds(30));
    }

    private static WorkflowTimerHostedService NewService(
        SqliteConnection connection,
        IWorkflowRuleInvoker invoker)
    {
        var provider = new ServiceCollection()
            .AddDbContext<VulperonexDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IWorkflowTimerRepository, WorkflowTimerRepository>()
            .AddSingleton(invoker)
            .BuildServiceProvider();

        return new WorkflowTimerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new AlwaysEnabledModuleStateService(),
            TimeProvider.System,
            NullLogger<WorkflowTimerHostedService>.Instance);
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
            Invocations.Add(new Invocation(workflowRuleId, streamEvent, invocationId));
            return Task.CompletedTask;
        }
    }

    private sealed record Invocation(string RuleId, IStreamEvent StreamEvent, string InvocationId);

    private sealed class AlwaysEnabledModuleStateService : IModuleStateService
    {
        public Task<IReadOnlyList<ModuleStateSnapshot>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModuleStateSnapshot>>([]);

        public Task<bool> IsEnabledAsync(string moduleName, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<ModuleToggleResult> ToggleAsync(string moduleName, bool enabled, string actorKind, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

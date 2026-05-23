using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows.Timers;

public sealed class WorkflowTimerHostedService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<WorkflowTimerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    public async Task<int> TickAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var timers = scope.ServiceProvider.GetRequiredService<IWorkflowTimerRepository>();
        var invoker = scope.ServiceProvider.GetRequiredService<IWorkflowRuleInvoker>();
        var dueTimers = await timers.ListDueAsync(now, cancellationToken).ConfigureAwait(false);

        foreach (var timer in dueTimers)
        {
            var invocationId = BuildInvocationId(timer);
            var streamEvent = new WorkflowSystemEvent
            {
                EventId = invocationId,
                EventTypeKey = StreamEventKeys.WorkflowTimer,
                Platform = "system",
                OccurredAt = now,
                Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["TimerId"] = timer.Id,
                    ["RuleId"] = timer.RuleId,
                    ["IntervalSeconds"] = timer.IntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
            };

            try
            {
                await invoker.InvokeAsync(
                    timer.RuleId,
                    streamEvent,
                    invocationId,
                    streamEvent.Payload,
                    cancellationToken).ConfigureAwait(false);
                await timers.MarkFiredAsync(
                    timer.Id,
                    timer.NextFireAt.AddSeconds(timer.IntervalSeconds),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Workflow timer {WorkflowTimerId} failed to fire.", timer.Id);
            }
        }

        return dueTimers.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await TickAsync(timeProvider.GetUtcNow(), stoppingToken).ConfigureAwait(false);
            await Task.Delay(TickInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static string BuildInvocationId(WorkflowTimer timer)
    {
        return $"timer:{timer.Id}:{timer.NextFireAt.ToUnixTimeMilliseconds()}";
    }
}

using System.Collections.Concurrent;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Time;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows;

public sealed class WorkflowEngine : IWorkflowRuleInvoker, IAsyncDisposable
{
    private readonly IStreamEventBus _eventBus;
    private readonly IWorkflowRuleQueryService _ruleQueryService;
    private readonly WorkflowConditionEvaluator _conditionEvaluator;
    private readonly IReadOnlyDictionary<string, IWorkflowActionExecutor> _executorsByType;
    private readonly IWorkflowActionExecutionStore _executionStore;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _ruleSemaphores = new();
    private IDisposable? _subscription;

    public WorkflowEngine(
        IStreamEventBus eventBus,
        IWorkflowRuleQueryService ruleQueryService,
        WorkflowConditionEvaluator conditionEvaluator,
        IEnumerable<IWorkflowActionExecutor> actionExecutors,
        IWorkflowActionExecutionStore executionStore,
        IClock clock)
    {
        _eventBus = eventBus;
        _ruleQueryService = ruleQueryService;
        _conditionEvaluator = conditionEvaluator;
        _executorsByType = actionExecutors.ToDictionary(executor => executor.ActionType, StringComparer.Ordinal);
        _executionStore = executionStore;
        _clock = clock;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _subscription = _eventBus.Subscribe<IStreamEvent>(HandleEventAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _subscription?.Dispose();
        _subscription = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task HandleEventAsync(IStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        var rules = await _ruleQueryService.ListEnabledByEventTypeAsync(streamEvent.EventTypeKey, cancellationToken);
        var orderedRules = rules
            .Where(rule => rule.IsEnabled && rule.EventTypeKey == streamEvent.EventTypeKey)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.CreatedAt)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal)
            .ToArray();

        await Task.WhenAll(orderedRules.Select(rule => ExecuteRuleAsync(rule, streamEvent, cancellationToken)));
    }

    public async Task ExecuteRuleAsync(
        WorkflowRule rule,
        IStreamEvent streamEvent,
        CancellationToken cancellationToken = default)
    {
        await ExecuteRuleAsync(rule, streamEvent, invocationId: null, cancellationToken);
    }

    public async Task InvokeAsync(
        string workflowRuleId,
        IStreamEvent streamEvent,
        string invocationId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _ruleQueryService.GetAsync(workflowRuleId, cancellationToken);
        if (rule is null)
        {
            return;
        }

        await ExecuteRuleAsync(rule, streamEvent, invocationId, cancellationToken);
    }

    private async Task ExecuteRuleAsync(
        WorkflowRule rule,
        IStreamEvent streamEvent,
        string? invocationId,
        CancellationToken cancellationToken = default)
    {
        if (!MatchesConditions(rule, streamEvent))
        {
            return;
        }

        var semaphore = _ruleSemaphores.GetOrAdd(
            rule.Id,
            _ => new SemaphoreSlim(rule.ExecutionMode is WorkflowExecutionMode.Parallel ? Math.Max(1, rule.MaxParallelism) : 1));

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (rule.ExecutionMode is WorkflowExecutionMode.Parallel)
            {
                await ExecuteActionsInParallelAsync(rule, streamEvent, invocationId, cancellationToken);
            }
            else
            {
                await ExecuteActionsSeriallyAsync(rule, streamEvent, invocationId, cancellationToken);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private bool MatchesConditions(WorkflowRule rule, IStreamEvent streamEvent)
    {
        var context = new ConditionEvaluationContext(streamEvent, rule.Id);
        return rule.Conditions.All(condition => _conditionEvaluator.IsMatch(condition, context));
    }

    private async Task ExecuteActionsSeriallyAsync(
        WorkflowRule rule,
        IStreamEvent streamEvent,
        string? invocationId,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < rule.Actions.Count; index++)
        {
            var shouldContinue = await ExecuteActionWithPolicyAsync(rule, streamEvent, index, invocationId, cancellationToken);
            if (!shouldContinue)
            {
                return;
            }
        }
    }

    private async Task ExecuteActionsInParallelAsync(
        WorkflowRule rule,
        IStreamEvent streamEvent,
        string? invocationId,
        CancellationToken cancellationToken)
    {
        using var throttle = new SemaphoreSlim(Math.Max(1, rule.MaxParallelism));
        var tasks = rule.Actions.Select(async (_, index) =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                await ExecuteActionWithPolicyAsync(rule, streamEvent, index, invocationId, cancellationToken);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task<bool> ExecuteActionWithPolicyAsync(
        WorkflowRule rule,
        IStreamEvent streamEvent,
        int actionIndex,
        string? invocationId,
        CancellationToken cancellationToken)
    {
        var action = rule.Actions[actionIndex];
        var key = new ActionExecutionKey(streamEvent.EventId, rule.Id, actionIndex, invocationId);

        if (!await _executionStore.TryBeginAsync(key, cancellationToken))
        {
            return true;
        }

        var maxAttempts = action.ErrorBehavior is ErrorBehavior.RetryOnError
            ? action.MaxRetries + 1
            : 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await ExecuteActionOnceAsync(
                    action,
                    new ActionExecutionContext(streamEvent, rule, actionIndex, invocationId),
                    cancellationToken);
                await _executionStore.MarkCompletedAsync(key, cancellationToken);
                return true;
            }
            catch when (action.ErrorBehavior is ErrorBehavior.RetryOnError && attempt < maxAttempts)
            {
                if (action.BackoffMs > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(action.BackoffMs), cancellationToken);
                }
            }
            catch
            {
                return action.ErrorBehavior is ErrorBehavior.ContinueOnError;
            }
        }

        return action.ErrorBehavior is ErrorBehavior.ContinueOnError;
    }

    private async Task ExecuteActionOnceAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!_executorsByType.TryGetValue(action.Type, out var executor))
        {
            return;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (action.TimeoutMs > 0)
        {
            timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(action.TimeoutMs));
        }

        await executor.ExecuteAsync(action, context, timeoutSource.Token);
    }
}

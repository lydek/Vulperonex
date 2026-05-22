using System.Collections.Concurrent;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Time;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows;

public sealed class WorkflowEngine : IWorkflowRuleInvoker, IAsyncDisposable
{
    private const int MinParallelism = 1;
    private const int MaxParallelismCap = 64;

    private readonly IStreamEventBus _eventBus;
    private readonly IWorkflowRuleQueryService _ruleQueryService;
    private readonly WorkflowConditionEvaluator _conditionEvaluator;
    private readonly IReadOnlyDictionary<string, IWorkflowActionExecutor> _executorsByType;
    private readonly IWorkflowActionExecutionStore _executionStore;
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _ruleSemaphores = new();
    private IDisposable? _subscription;

    public WorkflowEngine(
        IStreamEventBus eventBus,
        IWorkflowRuleQueryService ruleQueryService,
        WorkflowConditionEvaluator conditionEvaluator,
        IEnumerable<IWorkflowActionExecutor> actionExecutors,
        IWorkflowActionExecutionStore executionStore,
        IExpressionEvaluator expressionEvaluator,
        IClock clock)
    {
        _eventBus = eventBus;
        _ruleQueryService = ruleQueryService;
        _conditionEvaluator = conditionEvaluator;
        _executorsByType = actionExecutors.ToDictionary(executor => executor.ActionType, StringComparer.Ordinal);
        _executionStore = executionStore;
        _expressionEvaluator = expressionEvaluator;
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
        // Query contract: ListEnabledByEventTypeAsync returns only enabled rules whose
        // EventTypeKey matches. No additional filtering here.
        var rules = await _ruleQueryService.ListEnabledByEventTypeAsync(streamEvent.EventTypeKey, cancellationToken);
        var orderedRules = rules
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

        var capacity = ResolveCapacity(rule);
        // Cache key includes capacity + execution mode so an edited rule
        // does not reuse a semaphore sized for the previous configuration.
        // Note: superseded semaphores remain in the dictionary; leak is
        // bounded by (rule count × edits per rule × engine lifetime) and
        // is acceptable for the MVP in-process engine. A persistent
        // engine should evict entries keyed by inactive rule ids.
        var semaphoreKey = $"{rule.Id}|{rule.ExecutionMode}|{capacity}";
        var semaphore = _ruleSemaphores.GetOrAdd(semaphoreKey, _ => new SemaphoreSlim(capacity));

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

    private static int ResolveCapacity(WorkflowRule rule)
    {
        return rule.ExecutionMode is WorkflowExecutionMode.Parallel
            ? Math.Clamp(rule.MaxParallelism, MinParallelism, MaxParallelismCap)
            : 1;
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
        var stepOutputs = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rule.Actions.Count; index++)
        {
            var shouldContinue = await ExecuteActionWithPolicyAsync(
                rule,
                streamEvent,
                index,
                invocationId,
                stepOutputs,
                cancellationToken);
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
        var capacity = ResolveCapacity(rule);
        using var throttle = new SemaphoreSlim(capacity);
        var tasks = rule.Actions.Select(async (_, index) =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                await ExecuteActionWithPolicyAsync(
                    rule,
                    streamEvent,
                    index,
                    invocationId,
                    new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
                    cancellationToken);
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
        IDictionary<string, IReadOnlyDictionary<string, object?>> stepOutputs,
        CancellationToken cancellationToken)
    {
        var action = rule.Actions[actionIndex];
        var key = new ActionExecutionKey(streamEvent.EventId, rule.Id, actionIndex, invocationId);
        var expressionContext = BuildExpressionContext(streamEvent, stepOutputs);

        if (!await _executionStore.TryBeginAsync(key, cancellationToken))
        {
            // Already terminal (Completed or Failed) — SPEC §4.2 says replay must skip.
            return true;
        }

        try
        {
            if (!ShouldExecute(action, expressionContext))
            {
                await _executionStore.MarkSkippedAsync(key, CancellationToken.None);
                return true;
            }

            var maxAttempts = action.ErrorBehavior is ErrorBehavior.RetryOnError
                ? action.MaxRetries + 1
                : 1;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var result = await ExecuteActionOnceAsync(
                        action,
                        new ActionExecutionContext(streamEvent, rule, actionIndex, invocationId, expressionContext),
                        cancellationToken);
                    if (result.IsSkipped)
                    {
                        await _executionStore.MarkSkippedAsync(key, CancellationToken.None);
                        return true;
                    }

                    CaptureOutput(action, result, stepOutputs);
                    await _executionStore.MarkCompletedAsync(key, CancellationToken.None);
                    return true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    await _executionStore.MarkAbandonedAsync(key, CancellationToken.None);
                    throw;
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
                    // Terminal failure: record Failed so replay skips.
                    await _executionStore.MarkFailedAsync(key, CancellationToken.None);
                    return action.ErrorBehavior is ErrorBehavior.ContinueOnError;
                }
            }

            // Retries exhausted without success.
            await _executionStore.MarkFailedAsync(key, CancellationToken.None);
            return action.ErrorBehavior is ErrorBehavior.ContinueOnError;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _executionStore.MarkAbandonedAsync(key, CancellationToken.None);
            throw;
        }
    }

    private bool ShouldExecute(WorkflowAction action, ExpressionContext expressionContext)
    {
        if (string.IsNullOrWhiteSpace(action.ExecutionCondition))
        {
            return true;
        }

        return CoerceToBoolean(_expressionEvaluator.Evaluate(action.ExecutionCondition, expressionContext));
    }

    private static bool CoerceToBoolean(object? value)
    {
        return value switch
        {
            bool boolean => boolean,
            byte number => number != 0,
            short number => number != 0,
            int number => number != 0,
            long number => number != 0,
            float number => Math.Abs(number) > float.Epsilon,
            double number => Math.Abs(number) > double.Epsilon,
            decimal number => number != 0,
            string text when bool.TryParse(text, out var boolean) => boolean,
            _ => false,
        };
    }

    private static void CaptureOutput(
        WorkflowAction action,
        ActionExecutionResult result,
        IDictionary<string, IReadOnlyDictionary<string, object?>> stepOutputs)
    {
        if (string.IsNullOrWhiteSpace(action.OutputVariable) || result.OutputValues is null)
        {
            return;
        }

        stepOutputs[action.OutputVariable] = result.OutputValues;
    }

    private static ExpressionContext BuildExpressionContext(
        IStreamEvent streamEvent,
        IDictionary<string, IReadOnlyDictionary<string, object?>> stepOutputs)
    {
        var trigger = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EventId"] = streamEvent.EventId,
            ["EventTypeKey"] = streamEvent.EventTypeKey,
            ["Platform"] = streamEvent.Platform,
            ["OccurredAt"] = streamEvent.OccurredAt,
        };

        if (streamEvent is UserSentMessageEvent messageEvent)
        {
            trigger["MessageText"] = messageEvent.MessageText;
        }

        var member = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (streamEvent.User is not null)
        {
            member["Platform"] = streamEvent.User.Platform;
            member["UserId"] = streamEvent.User.UserId;
            member["DisplayName"] = streamEvent.User.DisplayName;
            member["Roles"] = streamEvent.User.Roles.ToString();
            member["IsSubscriber"] = streamEvent.User.Roles.HasFlag(StreamRole.Subscriber);
            member["IsModerator"] = streamEvent.User.Roles.HasFlag(StreamRole.Moderator);
            member["IsVip"] = streamEvent.User.Roles.HasFlag(StreamRole.Vip);
            member["IsFollower"] = streamEvent.User.Roles.HasFlag(StreamRole.Follower);
        }

        return new ExpressionContext(
            trigger,
            new Dictionary<string, IReadOnlyDictionary<string, object?>>(stepOutputs, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            member);
    }

    private async Task<ActionExecutionResult> ExecuteActionOnceAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!_executorsByType.TryGetValue(action.Type, out var executor))
        {
            return ActionExecutionResult.Completed;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (action.TimeoutMs > 0)
        {
            timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(action.TimeoutMs));
        }

        return await executor.ExecuteAsync(action, context, timeoutSource.Token);
    }
}

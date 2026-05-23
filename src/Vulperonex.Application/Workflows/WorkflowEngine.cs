using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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
    private const int MaxSystemEventDepth = 5;

    private readonly IStreamEventBus _eventBus;
    private readonly IRuleSnapshotCache _ruleSnapshotCache;
    private readonly WorkflowConditionEvaluator _conditionEvaluator;
    private readonly IReadOnlyDictionary<string, IWorkflowActionExecutor> _executorsByType;
    private readonly IWorkflowActionExecutionStore _executionStore;
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly IWorkflowThrottleService _throttleService;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _ruleSemaphores = new();
    private IDisposable? _subscription;

    public WorkflowEngine(
        IStreamEventBus eventBus,
        IRuleSnapshotCache ruleSnapshotCache,
        WorkflowConditionEvaluator conditionEvaluator,
        IEnumerable<IWorkflowActionExecutor> actionExecutors,
        IWorkflowActionExecutionStore executionStore,
        IExpressionEvaluator expressionEvaluator,
        IWorkflowThrottleService throttleService,
        IClock clock)
    {
        _eventBus = eventBus;
        _ruleSnapshotCache = ruleSnapshotCache;
        _conditionEvaluator = conditionEvaluator;
        _executorsByType = actionExecutors.ToDictionary(executor => executor.ActionType, StringComparer.Ordinal);
        _executionStore = executionStore;
        _expressionEvaluator = expressionEvaluator;
        _throttleService = throttleService;
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
        if (streamEvent is WorkflowSystemEvent { Depth: > MaxSystemEventDepth })
        {
            return;
        }

        // Query contract: ListEnabledByEventTypeAsync returns only enabled rules whose
        // EventTypeKey matches. No additional filtering here.
        var rules = await _ruleSnapshotCache.GetByEventTypeAsync(streamEvent.EventTypeKey, cancellationToken);
        var orderedRules = rules
            .Where(rule => !rule.IsSubWorkflow)
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
        await ExecuteRuleAsync(rule, streamEvent, invocationId: null, cancellationToken: cancellationToken);
    }

    public async Task InvokeAsync(
        string workflowRuleId,
        IStreamEvent streamEvent,
        string invocationId,
        IReadOnlyDictionary<string, string>? args = null,
        CancellationToken cancellationToken = default)
    {
        var rule = await _ruleSnapshotCache.GetByIdAsync(workflowRuleId, cancellationToken);
        if (rule is null)
        {
            return;
        }

        await ExecuteRuleAsync(rule, streamEvent, invocationId, args, cancellationToken);
    }

    private async Task ExecuteRuleAsync(
        WorkflowRule rule,
        IStreamEvent streamEvent,
        string? invocationId,
        IReadOnlyDictionary<string, string>? args = null,
        CancellationToken cancellationToken = default)
    {
        if (!MatchesConditions(rule, streamEvent) || !MatchesTrigger(rule, streamEvent))
        {
            return;
        }

        await using var throttleLease = await _throttleService.TryAcquireAsync(rule, streamEvent, cancellationToken);
        if (throttleLease is null)
        {
            return;
        }

        using var timeoutSource = CreateRuleTimeoutSource(rule, cancellationToken);
        var effectiveCancellationToken = timeoutSource?.Token ?? cancellationToken;

        var capacity = ResolveCapacity(rule);
        // Cache key includes capacity + execution mode so an edited rule
        // does not reuse a semaphore sized for the previous configuration.
        // Note: superseded semaphores remain in the dictionary; leak is
        // bounded by (rule count × edits per rule × engine lifetime) and
        // is acceptable for the MVP in-process engine. A persistent
        // engine should evict entries keyed by inactive rule ids.
        var semaphoreKey = $"{rule.Id}|{rule.ExecutionMode}|{capacity}";
        var semaphore = _ruleSemaphores.GetOrAdd(semaphoreKey, _ => new SemaphoreSlim(capacity));

        await semaphore.WaitAsync(effectiveCancellationToken);
        try
        {
            WorkflowPipelineResult result;
            if (rule.ExecutionMode is WorkflowExecutionMode.Parallel)
            {
                result = await ExecuteActionsInParallelAsync(rule, streamEvent, invocationId, effectiveCancellationToken);
            }
            else
            {
                result = await ExecuteActionsSeriallyAsync(
                    rule,
                    rule.Actions,
                    streamEvent,
                    invocationId,
                    WorkflowExecutionPhase.Main,
                    failure: null,
                    args ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    effectiveCancellationToken);
            }

            if (!result.Succeeded)
            {
                await ExecuteOnFailureStepsAsync(rule, streamEvent, invocationId, result, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (timeoutSource?.IsCancellationRequested is true
            && !cancellationToken.IsCancellationRequested)
        {
            await ExecuteOnFailureStepsAsync(
                rule,
                streamEvent,
                invocationId,
                WorkflowPipelineResult.Failed(-1, "Rule timed out."),
                cancellationToken);
            return;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static CancellationTokenSource? CreateRuleTimeoutSource(
        WorkflowRule rule,
        CancellationToken cancellationToken)
    {
        if (rule.TimeoutSeconds <= 0)
        {
            return null;
        }

        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(rule.TimeoutSeconds));
        return timeoutSource;
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

    private bool MatchesTrigger(WorkflowRule rule, IStreamEvent streamEvent)
    {
        var expressionContext = BuildExpressionContext(
            streamEvent,
            new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
            failure: null);

        var trigger = rule.Trigger;
        if (trigger is not null && !MatchesTriggerFilter(trigger.Filter, expressionContext.Trigger))
        {
            return false;
        }

        var matchCondition = rule.MatchCondition ?? trigger?.MatchCondition;
        return string.IsNullOrWhiteSpace(matchCondition)
            || CoerceToBoolean(_expressionEvaluator.Evaluate(matchCondition, expressionContext));
    }

    private static bool MatchesTriggerFilter(
        IReadOnlyDictionary<string, string> filter,
        IReadOnlyDictionary<string, object?> triggerValues)
    {
        foreach (var (key, expected) in filter)
        {
            if (!triggerValues.TryGetValue(key, out var actual)
                || !string.Equals(actual?.ToString(), expected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<WorkflowPipelineResult> ExecuteActionsSeriallyAsync(
        WorkflowRule rule,
        IReadOnlyList<WorkflowAction> actions,
        IStreamEvent streamEvent,
        string? invocationId,
        WorkflowExecutionPhase phase,
        WorkflowFailureContext? failure,
        IReadOnlyDictionary<string, string> args,
        CancellationToken cancellationToken)
    {
        var stepOutputs = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < actions.Count; index++)
        {
            var result = await ExecuteActionWithPolicyAsync(
                rule,
                actions,
                streamEvent,
                index,
                invocationId,
                phase,
                stepOutputs,
                failure,
                args,
                cancellationToken);
            if (!result.Succeeded)
            {
                return result;
            }

            if (result.StoppedGracefully)
            {
                return result;
            }
        }

        return WorkflowPipelineResult.Success;
    }

    private async Task<WorkflowPipelineResult> ExecuteActionsInParallelAsync(
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
                return await ExecuteActionWithPolicyAsync(
                    rule,
                    rule.Actions,
                    streamEvent,
                    index,
                    invocationId,
                    WorkflowExecutionPhase.Main,
                    new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
                    failure: null,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    cancellationToken);
            }
            finally
            {
                throttle.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.FirstOrDefault(result => !result.Succeeded) ?? WorkflowPipelineResult.Success;
    }

    private async Task<WorkflowPipelineResult> ExecuteActionWithPolicyAsync(
        WorkflowRule rule,
        IReadOnlyList<WorkflowAction> actions,
        IStreamEvent streamEvent,
        int actionIndex,
        string? invocationId,
        WorkflowExecutionPhase phase,
        IDictionary<string, IReadOnlyDictionary<string, object?>> stepOutputs,
        WorkflowFailureContext? failure,
        IReadOnlyDictionary<string, string> args,
        CancellationToken cancellationToken)
    {
        var action = actions[actionIndex];
        var key = new ActionExecutionKey(streamEvent.EventId, rule.Id, actionIndex, invocationId, phase);
        var expressionContext = BuildExpressionContext(streamEvent, stepOutputs, failure, args);

        if (!await _executionStore.TryBeginAsync(key, cancellationToken))
        {
            // Already terminal (Completed or Failed) — SPEC §4.2 says replay must skip.
            return WorkflowPipelineResult.Success;
        }

        try
        {
            if (!ShouldExecute(action, expressionContext))
            {
                await _executionStore.MarkSkippedAsync(key, CancellationToken.None);
                return WorkflowPipelineResult.Success;
            }

            var actionContext = new ActionExecutionContext(streamEvent, rule, actionIndex, invocationId, expressionContext, phase);
            ActionExecutionResult result;
            try
            {
                result = await BuildAttemptPipeline(action, actionContext, cancellationToken)
                    .ToTask(cancellationToken);
            }
            catch (WorkflowGracefulStopException)
            {
                await _executionStore.MarkCompletedAsync(key, CancellationToken.None);
                return WorkflowPipelineResult.GracefulStop;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await _executionStore.MarkAbandonedAsync(key, CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                await _executionStore.MarkFailedAsync(key, CancellationToken.None);
                return action.ErrorBehavior is ErrorBehavior.ContinueOnError
                    ? WorkflowPipelineResult.Success
                    : WorkflowPipelineResult.Failed(
                        actionIndex,
                        ex is TimeoutException ? "Action timed out." : ex.Message);
            }

            if (result.IsSkipped)
            {
                await _executionStore.MarkSkippedAsync(key, CancellationToken.None);
                return WorkflowPipelineResult.Success;
            }

            CaptureOutput(action, result, stepOutputs);
            await _executionStore.MarkCompletedAsync(key, CancellationToken.None);
            return WorkflowPipelineResult.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _executionStore.MarkAbandonedAsync(key, CancellationToken.None);
            throw;
        }
    }

    private async Task ExecuteOnFailureStepsAsync(
        WorkflowRule rule,
        IStreamEvent streamEvent,
        string? invocationId,
        WorkflowPipelineResult failure,
        CancellationToken cancellationToken)
    {
        if (rule.OnFailureSteps.Count is 0)
        {
            return;
        }

        await ExecuteActionsSeriallyAsync(
            rule,
            rule.OnFailureSteps,
            streamEvent,
            invocationId,
            WorkflowExecutionPhase.OnFailure,
            new WorkflowFailureContext(failure.FailureStepIndex ?? -1, failure.ErrorMessage ?? string.Empty),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            cancellationToken);
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
        IDictionary<string, IReadOnlyDictionary<string, object?>> stepOutputs,
        WorkflowFailureContext? failure,
        IReadOnlyDictionary<string, string>? args = null)
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

        if (streamEvent is RewardRedeemedEvent rewardEvent)
        {
            trigger["RewardId"] = rewardEvent.RewardId;
            trigger["RewardTitle"] = rewardEvent.RewardTitle;
            trigger["RedemptionId"] = rewardEvent.RedemptionId;
        }

        if (streamEvent is WorkflowSystemEvent systemEvent)
        {
            trigger["Depth"] = systemEvent.Depth;
            trigger["Payload"] = systemEvent.Payload;
            foreach (var (key, value) in systemEvent.Payload)
            {
                trigger[$"Payload.{key}"] = value;
            }
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

        var failureValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (failure is not null)
        {
            failureValues["StepIndex"] = failure.StepIndex;
            failureValues["ErrorMessage"] = failure.ErrorMessage;
        }

        return new ExpressionContext(
            trigger,
            new Dictionary<string, IReadOnlyDictionary<string, object?>>(stepOutputs, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(args ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
            member,
            failureValues);
    }

    private IObservable<ActionExecutionResult> BuildAttemptPipeline(
        WorkflowAction action,
        ActionExecutionContext actionContext,
        CancellationToken cancellationToken)
    {
        var maxRetries = action.ErrorBehavior is ErrorBehavior.RetryOnError ? action.MaxRetries : 0;
        var backoff = TimeSpan.FromMilliseconds(Math.Max(0, action.BackoffMs));
        var timeout = action.TimeoutMs > 0
            ? TimeSpan.FromMilliseconds(action.TimeoutMs)
            : (TimeSpan?)null;

        // Each Rx subscription = one attempt. RetryWhen re-subscribes, so
        // Timeout and the FromAsync cancellation token are fresh per try.
        var attempt = Observable.FromAsync(ct =>
        {
            if (!_executorsByType.TryGetValue(action.Type, out var executor))
            {
                return Task.FromResult(ActionExecutionResult.Completed);
            }
            return executor.ExecuteAsync(action, actionContext, ct);
        });

        if (timeout is { } span)
        {
            attempt = attempt.Timeout(span);
        }

        return attempt.RetryWhen(errors => errors
            .Select((err, index) => (err, retried: index))
            .SelectMany(x =>
            {
                // GracefulStop and outer cancellation propagate immediately.
                if (x.err is WorkflowGracefulStopException)
                {
                    return Observable.Throw<long>(x.err);
                }
                if (x.err is OperationCanceledException && cancellationToken.IsCancellationRequested)
                {
                    return Observable.Throw<long>(x.err);
                }
                if (x.retried >= maxRetries)
                {
                    return Observable.Throw<long>(x.err);
                }
                return backoff > TimeSpan.Zero
                    ? Observable.Timer(backoff)
                    : Observable.Return(0L);
            }));
    }

    private sealed record WorkflowFailureContext(int StepIndex, string ErrorMessage);

    private sealed record WorkflowPipelineResult(
        bool Succeeded,
        bool StoppedGracefully = false,
        int? FailureStepIndex = null,
        string? ErrorMessage = null)
    {
        public static WorkflowPipelineResult Success { get; } = new(true);

        public static WorkflowPipelineResult GracefulStop { get; } = new(true, StoppedGracefully: true);

        public static WorkflowPipelineResult Failed(int failureStepIndex, string errorMessage)
        {
            return new WorkflowPipelineResult(
                Succeeded: false,
                FailureStepIndex: failureStepIndex,
                ErrorMessage: errorMessage);
        }
    }
}

using System.Collections.Concurrent;
using Vulperonex.Application.Time;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows;

public sealed class InMemoryWorkflowThrottleService(IClock clock) : IWorkflowThrottleService
{
    private readonly ConcurrentDictionary<string, RuleThrottleState> _states = new(StringComparer.Ordinal);

    public Task<IAsyncDisposable?> TryAcquireAsync(
        WorkflowRule rule,
        IStreamEvent streamEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = _states.GetOrAdd(rule.Id, _ => new RuleThrottleState());
        return Task.FromResult(state.TryAcquire(rule, streamEvent, clock.UtcNow));
    }

    private sealed class RuleThrottleState
    {
        private readonly Dictionary<string, DateTimeOffset> _lastFireByUser = new(StringComparer.OrdinalIgnoreCase);
        private int _running;
        private DateTimeOffset? _lastFireAt;

        public IAsyncDisposable? TryAcquire(WorkflowRule rule, IStreamEvent streamEvent, DateTimeOffset now)
        {
            var skipCooldown = streamEvent is Vulperonex.Domain.Events.ICooldownSkippable { SkipCooldown: true } &&
                               streamEvent.Platform == "simulation";

            lock (this)
            {
                if (IsConcurrencyRejected(rule.Throttle))
                {
                    return null;
                }

                if (!skipCooldown)
                {
                    if (IsGlobalCooldownRejected(rule.Throttle, now))
                    {
                        return null;
                    }

                    var userKey = ResolveUserKey(streamEvent);
                    if (IsPerUserCooldownRejected(rule.Throttle, userKey, now))
                    {
                        return null;
                    }
                }

                _running++;
                _lastFireAt = now;
                var uKey = ResolveUserKey(streamEvent);
                if (rule.Throttle.PerUserCooldown && uKey is not null)
                {
                    _lastFireByUser[uKey] = now;
                }

                return new Lease(this);
            }
        }

        private bool IsConcurrencyRejected(WorkflowThrottlePolicy throttle)
        {
            return throttle.MaxConcurrent > 0 && _running >= throttle.MaxConcurrent;
        }

        private bool IsGlobalCooldownRejected(WorkflowThrottlePolicy throttle, DateTimeOffset now)
        {
            return throttle.CooldownSeconds > 0
                && _lastFireAt is not null
                && now < _lastFireAt.Value.AddSeconds(throttle.CooldownSeconds);
        }

        private bool IsPerUserCooldownRejected(
            WorkflowThrottlePolicy throttle,
            string? userKey,
            DateTimeOffset now)
        {
            return throttle.PerUserCooldown
                && throttle.PerUserCooldownSeconds > 0
                && userKey is not null
                && _lastFireByUser.TryGetValue(userKey, out var lastFireAt)
                && now < lastFireAt.AddSeconds(throttle.PerUserCooldownSeconds);
        }

        private static string? ResolveUserKey(IStreamEvent streamEvent)
        {
            return streamEvent.User is null
                ? null
                : $"{streamEvent.User.Platform}:{streamEvent.User.UserId}";
        }

        private sealed class Lease(RuleThrottleState owner) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                lock (owner)
                {
                    owner._running = Math.Max(0, owner._running - 1);
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}

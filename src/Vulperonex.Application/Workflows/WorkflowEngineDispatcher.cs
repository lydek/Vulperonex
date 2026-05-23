using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.EventBus;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows;

/// <summary>
/// Bridges the host-lifetime <see cref="IStreamEventBus"/> to the scoped
/// <see cref="WorkflowEngine"/>. The engine is registered as Scoped because
/// it depends on the rule snapshot cache, execution store, and per-request
/// action executors. A long-lived bus subscription cannot itself be scoped,
/// so this dispatcher subscribes once at host startup and opens a fresh
/// service scope per event before resolving the engine. Without this
/// hosted service the engine would never see bus events in production --
/// only timer- or sub-workflow-initiated invocations would execute.
/// </summary>
public sealed class WorkflowEngineDispatcher(
    IStreamEventBus eventBus,
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowEngineDispatcher> logger) : IHostedService
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _subscription = eventBus.Subscribe<IStreamEvent>(DispatchAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        return Task.CompletedTask;
    }

    private async Task DispatchAsync(IStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<WorkflowEngine>();
            await engine.ProcessEventAsync(streamEvent, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown cancellation must not escape into the bus dispatch loop.
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "WorkflowEngine failed to process event {EventId} of type {EventTypeKey}.",
                streamEvent.EventId,
                streamEvent.EventTypeKey);
        }
    }
}

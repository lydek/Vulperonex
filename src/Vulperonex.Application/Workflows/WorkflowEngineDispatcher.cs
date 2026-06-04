using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Modules;
using Vulperonex.Application.Settings;
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
    IModuleStateService moduleStateService,
    IObservable<SettingChangedEvent> settingChanges,
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowEngineDispatcher> logger) : IHostedService
{
    private IDisposable? _subscription;
    private IDisposable? _settingSubscription;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _settingSubscription = settingChanges.Subscribe(new SettingObserver(this));
        if (await moduleStateService.IsEnabledAsync("workflow", cancellationToken).ConfigureAwait(false))
        {
            _subscription = eventBus.Subscribe<IStreamEvent>(DispatchAsync);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        _settingSubscription?.Dispose();
        _settingSubscription = null;
        return Task.CompletedTask;
    }

    private Task DispatchAsync(IStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        _ = Task.Run(
            () => DispatchInBackgroundAsync(streamEvent, cancellationToken),
            CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task DispatchInBackgroundAsync(IStreamEvent streamEvent, CancellationToken cancellationToken)
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
        catch (DependencyMissingException ex)
        {
            logger.LogWarning("Workflow engine blocked due to disabled dependency: {Message}", ex.Message);
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

    private async void ApplySettingChangeAsync(SettingChangedEvent changedEvent)
    {
        if (!string.Equals(changedEvent.Key, SystemSettingKey.ModuleEnabled("workflow"), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var enabled = await moduleStateService.IsEnabledAsync("workflow").ConfigureAwait(false);
            if (enabled)
            {
                _subscription ??= eventBus.Subscribe<IStreamEvent>(DispatchAsync);
                return;
            }

            _subscription?.Dispose();
            _subscription = null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply setting change in WorkflowEngineDispatcher.");
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed class SettingObserver(WorkflowEngineDispatcher dispatcher) : IObserver<SettingChangedEvent>
    {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(SettingChangedEvent value)
        {
            dispatcher.ApplySettingChangeAsync(value);
        }
    }
}

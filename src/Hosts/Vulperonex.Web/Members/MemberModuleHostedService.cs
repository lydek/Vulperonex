using Microsoft.Extensions.Logging;
using System.Threading;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Members;
using Vulperonex.Application.Modules;
using Vulperonex.Application.Settings;

namespace Vulperonex.Web.Members;

public sealed class MemberModuleHostedService(
    IServiceProvider serviceProvider,
    IModuleStateService moduleStateService,
    IObservable<SettingChangedEvent> settingChanges,
    ILogger<MemberModuleHostedService> logger) : IHostedService, IAsyncDisposable
{
    private AsyncServiceScope? _scope;
    private MemberModule? _module;
    private IDisposable? _subscription;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = settingChanges.Subscribe(new SettingObserver(this));
        if (await moduleStateService.IsEnabledAsync("member", cancellationToken).ConfigureAwait(false))
        {
            await EnsureModuleRunningAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        await StopModuleAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _subscription?.Dispose();
        await StopModuleAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task EnsureModuleRunningAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            return;
        }

        _scope = serviceProvider.CreateAsyncScope();
        var services = _scope.Value.ServiceProvider;
        _module = new MemberModule(
            services.GetRequiredService<IStreamEventBus>(),
            services.GetRequiredService<IMemberResolver>(),
            services.GetRequiredService<IMemberStreamStateRepository>(),
            services.GetRequiredService<TimeProvider>());
        await _module.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task StopModuleAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            try
            {
                await _module.StopAsync(cancellationToken).ConfigureAwait(false);
                _module.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error stopping member module.");
            }
            finally
            {
                _module = null;
            }
        }

        if (_scope is not null)
        {
            try
            {
                await _scope.Value.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Catch SQLite's flaky NullReferenceException on connection close during fast test shutdown
                logger.LogDebug(ex, "Error disposing member module scope.");
            }
            finally
            {
                _scope = null;
            }
        }
    }

    private async void ApplySettingChangeAsync(SettingChangedEvent changedEvent)
    {
        if (!string.Equals(changedEvent.Key, SystemSettingKey.ModuleEnabled("member"), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (await moduleStateService.IsEnabledAsync("member").ConfigureAwait(false))
            {
                await EnsureModuleRunningAsync(CancellationToken.None).ConfigureAwait(false);
                return;
            }

            await StopModuleAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply setting change in MemberModuleHostedService.");
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed class SettingObserver(MemberModuleHostedService service) : IObserver<SettingChangedEvent>
    {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(SettingChangedEvent value)
        {
            service.ApplySettingChangeAsync(value);
        }
    }
}

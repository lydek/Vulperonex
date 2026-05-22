using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Vulperonex.Application.Settings;

namespace Vulperonex.Web.Logging;

/// <summary>
/// Polls the SystemSettings table for the configured Serilog minimum level
/// and applies it to the shared <see cref="LoggingLevelSwitch"/> so operators
/// can change verbosity at runtime without restarting the host.
///
/// A polling pull is used instead of the per-scope ISystemSettingsService
/// observable because that observable only fires for the scope that wrote
/// the setting -- across scopes (which the CLI / Web UI both use) the event
/// would otherwise be invisible.
/// </summary>
public sealed class LogLevelHotReloadWorker(
    IServiceScopeFactory scopeFactory,
    LoggingLevelSwitch levelSwitch,
    ILogger<LogLevelHotReloadWorker> logger) : BackgroundService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    private string? _lastApplied;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read log.min_level from settings; will retry.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        var configured = await settings
            .GetAsync<string?>(SystemSettingKey.LogMinLevel, defaultValue: null, cancellationToken)
            .ConfigureAwait(false);

        if (configured is null || string.Equals(configured, _lastApplied, StringComparison.OrdinalIgnoreCase))
        {
            _lastApplied ??= configured;
            return;
        }

        SerilogConfigurator.ApplyConfiguredLevel(levelSwitch, configured);
        _lastApplied = configured;
        logger.LogInformation("Applied Serilog minimum level {Level} from system settings.", configured);
    }
}

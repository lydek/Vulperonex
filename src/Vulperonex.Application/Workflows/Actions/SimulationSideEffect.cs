using Vulperonex.Application.Settings;

namespace Vulperonex.Application.Workflows.Actions;

/// <summary>
/// Helper for the simulation side-effect policy (feature spec §4.27). Persistent-write actions
/// (check-in, counters, lottery) consult this to decide whether a simulated event should skip its
/// real database write. External Twitch API actions (shoutout / refund / lookup) do NOT use this —
/// they always skip in simulation regardless of any setting.
/// </summary>
internal static class SimulationSideEffect
{
    /// <summary>
    /// True when the triggering event is simulated AND persistent writes are not explicitly allowed.
    /// The <see cref="SystemSettingKey.SimulationAllowPersistentWrites"/> toggle defaults to false, so
    /// simulation suppresses real writes unless an operator opts in. A null settings service (e.g. in
    /// unit tests constructed without it) is treated as "not allowed" — the safe default.
    /// </summary>
    public static async Task<bool> ShouldSuppressPersistentWriteAsync(
        ActionExecutionContext context,
        ISystemSettingsService? settings,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(context.StreamEvent.Platform, "simulation", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var allow = settings is not null
            && await settings
                .GetAsync(SystemSettingKey.SimulationAllowPersistentWrites, false, cancellationToken)
                .ConfigureAwait(false);
        return !allow;
    }
}

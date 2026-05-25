using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Modules;
using Vulperonex.Application.Settings;

namespace Vulperonex.Infrastructure.Modules;

public sealed class ModuleStateService : IModuleStateService, IDisposable
{
    private static readonly IReadOnlyList<ModuleDefinition> Definitions =
    [
        new("workflow", "Workflow Engine", "core", []),
        new("member", "Member Module", "core", []),
        new("checkin", "Check-In Module", "core", ["workflow", "member"]),
        new("lottery", "Lottery Module", "core", ["workflow", "member"]),
        new("onecommebridge", "OneComme Bridge", "plugin", []),
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModuleStateService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, ModuleDefinition> _definitionsByName = Definitions.ToDictionary(
        definition => definition.Name,
        StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _dependentsByName = BuildDependents(Definitions);
    private readonly Dictionary<string, bool> _enabledByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _enabledCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDisposable _subscription;
    private bool _initialised;

    public ModuleStateService(
        IServiceScopeFactory scopeFactory,
        IObservable<SettingChangedEvent> changes,
        ILogger<ModuleStateService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _subscription = changes.Subscribe(new SettingObserver(this));
    }

    public async Task<IReadOnlyList<ModuleStateSnapshot>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitialisedAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Definitions.Select(BuildSnapshot).ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> IsEnabledAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        await EnsureInitialisedAsync(cancellationToken).ConfigureAwait(false);
        moduleName = NormalizeModuleName(moduleName);
        if (!_definitionsByName.ContainsKey(moduleName))
        {
            throw new KeyNotFoundException($"Module '{moduleName}' is not defined.");
        }
        return _enabledCache.TryGetValue(moduleName, out var enabled)
            ? enabled
            : _definitionsByName[moduleName].EnabledByDefault;
    }

    public async Task<ModuleToggleResult> ToggleAsync(
        string moduleName,
        bool enabled,
        string actorKind,
        CancellationToken cancellationToken = default)
    {
        moduleName = NormalizeModuleName(moduleName);
        await EnsureInitialisedAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<string, bool> changes;
        var beforeStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var afterStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            changes = enabled
                ? ComputeEnableChangesUnsafe(moduleName)
                : ComputeDisableChangesUnsafe(moduleName);

            if (changes.Count == 0)
            {
                return new ModuleToggleResult(BuildSnapshot(_definitionsByName[moduleName]), []);
            }

            foreach (var changeKey in changes.Keys)
            {
                beforeStates[changeKey] = GetEnabledUnsafe(changeKey);
                afterStates[changeKey] = changes[changeKey];
            }
        }
        finally
        {
            _gate.Release();
        }

        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        foreach (var change in changes.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            await settings.SetAsync(
                SystemSettingKey.ModuleEnabled(change.Key),
                change.Value,
                "modules",
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Module {ModuleName} changed to {Enabled}. ActorKind={ActorKind} ActionType={ActionType}",
                change.Key,
                change.Value,
                actorKind,
                change.Value ? "enable_module" : "disable_module");
        }

        // Write append-only MemberAuditLogs trace
        try
        {
            var auditLogRepository = scope.ServiceProvider.GetRequiredService<Vulperonex.Application.Members.IMemberAuditLogRepository>();
            var audit = new Vulperonex.Application.Members.MemberAuditLog
            {
                MemberId = moduleName,
                SubjectKind = "module",
                ActorKind = actorKind,
                ActorId = null,
                Operation = enabled ? "enable_module" : "disable_module",
                BeforeJson = JsonSerializer.Serialize(beforeStates),
                AfterJson = JsonSerializer.Serialize(afterStates),
                Reason = $"Toggle module {moduleName} to {enabled}",
                OccurredAt = DateTimeOffset.UtcNow
            };
            await auditLogRepository.AppendAsync(audit, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for module toggle in ModuleStateService.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var change in changes)
            {
                _enabledByName[change.Key] = change.Value;
                _enabledCache[change.Key] = change.Value;
            }

            var changedModules = changes.Keys
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => BuildSnapshot(_definitionsByName[name]))
                .ToArray();

            return new ModuleToggleResult(
                BuildSnapshot(_definitionsByName[moduleName]),
                changedModules);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _gate.Dispose();
    }

    private async Task EnsureInitialisedAsync(CancellationToken cancellationToken)
    {
        if (_initialised)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialised)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            foreach (var definition in Definitions)
            {
                var enabled = await settings
                    .GetAsync(SystemSettingKey.ModuleEnabled(definition.Name), definition.EnabledByDefault, cancellationToken)
                    .ConfigureAwait(false);
                _enabledByName[definition.Name] = enabled;
                _enabledCache[definition.Name] = enabled;
            }

            _initialised = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private ModuleStateSnapshot BuildSnapshot(ModuleDefinition definition)
    {
        return new ModuleStateSnapshot(
            definition.Name,
            definition.DisplayName,
            definition.Kind,
            GetEnabledUnsafe(definition.Name),
            definition.Dependencies.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            _dependentsByName[definition.Name].OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private bool GetEnabledUnsafe(string moduleName)
    {
        moduleName = NormalizeModuleName(moduleName);
        return _enabledByName.TryGetValue(moduleName, out var enabled)
            ? enabled
            : _definitionsByName[moduleName].EnabledByDefault;
    }

    private Dictionary<string, bool> ComputeEnableChangesUnsafe(string moduleName)
    {
        var changes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        EnableRecursive(moduleName, changes);
        return changes;
    }

    private void EnableRecursive(string moduleName, Dictionary<string, bool> changes)
    {
        foreach (var dependency in _definitionsByName[moduleName].Dependencies)
        {
            EnableRecursive(dependency, changes);
        }

        if (!GetEnabledUnsafe(moduleName))
        {
            changes[moduleName] = true;
        }
    }

    private Dictionary<string, bool> ComputeDisableChangesUnsafe(string moduleName)
    {
        var changes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        DisableRecursive(moduleName, changes);
        return changes;
    }

    private void DisableRecursive(string moduleName, Dictionary<string, bool> changes)
    {
        foreach (var dependent in _dependentsByName[moduleName])
        {
            DisableRecursive(dependent, changes);
        }

        if (GetEnabledUnsafe(moduleName))
        {
            changes[moduleName] = false;
        }
    }

    private static Dictionary<string, List<string>> BuildDependents(IReadOnlyList<ModuleDefinition> definitions)
    {
        var map = definitions.ToDictionary(
            definition => definition.Name,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            foreach (var dependency in definition.Dependencies)
            {
                map[dependency].Add(definition.Name);
            }
        }

        return map;
    }

    private static string NormalizeModuleName(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new ArgumentException("Module name must not be empty.", nameof(moduleName));
        }

        return moduleName.Trim().ToLowerInvariant();
    }

    private async void ApplySettingChangeAsync(SettingChangedEvent changedEvent)
    {
        try
        {
            if (!SystemSettingKey.TryParseModuleEnabledKey(changedEvent.Key, out var moduleName))
            {
                return;
            }

            if (moduleName is null)
            {
                return;
            }

            if (!_definitionsByName.ContainsKey(moduleName))
            {
                return;
            }

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var val = ParseBool(changedEvent.NewValue, _definitionsByName[moduleName].EnabledByDefault);
                _enabledByName[moduleName] = val;
                _enabledCache[moduleName] = val;
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply setting change in ModuleStateService.");
        }
    }

    private static bool ParseBool(string? rawJson, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<bool>(rawJson);
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    private sealed class SettingObserver(ModuleStateService service) : IObserver<SettingChangedEvent>
    {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(SettingChangedEvent value)
        {
            service.ApplySettingChangeAsync(value);
        }
    }
}

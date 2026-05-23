using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Filters;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Logging;

namespace Vulperonex.Web.Logging;

public static class SerilogConfigurator
{
    public const string DefaultMinLevel = "Information";

    public static LoggingLevelSwitch CreateLevelSwitch(string? configured)
    {
        return new LoggingLevelSwitch(ParseLevel(configured));
    }

    public static void ApplyConfiguredLevel(LoggingLevelSwitch levelSwitch, string? configured)
    {
        levelSwitch.MinimumLevel = ParseLevel(configured);
    }

    public static LogEventLevel ParseLevel(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return LogEventLevel.Information;
        }

        return Enum.TryParse<LogEventLevel>(configured.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : LogEventLevel.Information;
    }

    public static string ResolveLogDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share");
        }

        var directory = Path.Combine(localAppData, "Vulperonex", "logs");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static LoggerConfiguration BuildLoggerConfiguration(
        LoggingLevelSwitch levelSwitch,
        AppLogsSink appLogsSink,
        string logDirectory)
    {
        return ConfigureLogger(new LoggerConfiguration(), levelSwitch, appLogsSink, logDirectory);
    }

    public static LoggerConfiguration ConfigureLogger(
        LoggerConfiguration configuration,
        LoggingLevelSwitch levelSwitch,
        AppLogsSink appLogsSink,
        string logDirectory)
    {
        return configuration
            .MinimumLevel.ControlledBy(levelSwitch)
            // EF Core's Database.Command source logs every SQL roundtrip at
            // Information. With several BackgroundService workers polling the
            // SQLite DB on 1-10s intervals, this floods the rolling file sink
            // and saturates the disk. Raise EF's floor to Warning so command
            // execution stays silent on the happy path but failures still
            // surface.
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logDirectory, "vulperonex-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 50L * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate:
                    "[{Timestamp:yyyy-MM-ddTHH:mm:ss.fffzzz} {Level:u3}] {Message:lj} "
                    + "{EventTypeKey} {Platform} {MemberId} {WorkflowRuleId} {ActionType}{NewLine}{Exception}")
            // AppLogsSink writes through EF Core, which itself logs SQL via
            // Serilog. Without this filter, every persisted batch generates new
            // EF "Executed DbCommand" events that re-enter the sink and create
            // an unbounded feedback loop. Route the sink through a sub-logger
            // that drops EF Core's own diagnostic stream.
            .WriteTo.Logger(sub => sub
                .Filter.ByExcluding(Matching.FromSource("Microsoft.EntityFrameworkCore"))
                .WriteTo.Sink(appLogsSink));
    }

    public static IDisposable BindHotReload(
        LoggingLevelSwitch levelSwitch,
        ISystemSettingsService settings)
    {
        return settings.Changes.Subscribe(new LevelSwitchObserver(levelSwitch));
    }

    private sealed class LevelSwitchObserver(LoggingLevelSwitch levelSwitch) : IObserver<SettingChangedEvent>
    {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(SettingChangedEvent value)
        {
            if (!string.Equals(value.Key, SystemSettingKey.LogMinLevel, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var raw = value.NewValue;
            // SystemSettingsService stores values as JSON. Trim wrapping quotes
            // when present so "Warning" and Warning both round-trip cleanly.
            if (!string.IsNullOrEmpty(raw) && raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            {
                raw = raw[1..^1];
            }

            ApplyConfiguredLevel(levelSwitch, raw);
        }
    }
}

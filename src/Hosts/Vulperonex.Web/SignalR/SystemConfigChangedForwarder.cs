using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using Vulperonex.Application.Settings;

namespace Vulperonex.Web.SignalR;

public sealed class SystemConfigChangedForwarder(
    IObservable<SettingChangedEvent> changes,
    IHubContext<EventsHub> eventsHub,
    ILogger<SystemConfigChangedForwarder> logger) : IHostedService
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = changes.Subscribe(new Observer(eventsHub, logger));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        return Task.CompletedTask;
    }

    private sealed class Observer(
        IHubContext<EventsHub> eventsHub,
        ILogger<SystemConfigChangedForwarder> logger) : IObserver<SettingChangedEvent>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(SettingChangedEvent value)
        {
            _ = PublishAsync(value);
        }

        private async Task PublishAsync(SettingChangedEvent value)
        {
            try
            {
                var envelope = new StreamEventEnvelope(
                    "system.config_changed",
                    $"cfg-{Guid.NewGuid():N}",
                    "system",
                    DateTimeOffset.UtcNow,
                    value.Key,
                    NormalizeValue(value.NewValue));

                await eventsHub.Clients.All.SendAsync("event", envelope);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to broadcast system.config_changed for {Key}.", value.Key);
            }
        }

        private static string? NormalizeValue(string? serializedValue)
        {
            if (string.IsNullOrWhiteSpace(serializedValue))
            {
                return serializedValue;
            }

            try
            {
                using var document = JsonDocument.Parse(serializedValue);
                return document.RootElement.ValueKind switch
                {
                    JsonValueKind.String => document.RootElement.GetString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Number => document.RootElement.ToString(),
                    JsonValueKind.Null => null,
                    _ => document.RootElement.GetRawText(),
                };
            }
            catch (JsonException)
            {
                return serializedValue;
            }
        }
    }
}

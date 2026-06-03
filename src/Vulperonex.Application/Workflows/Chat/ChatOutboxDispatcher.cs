using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Workflows.Actions;

namespace Vulperonex.Application.Workflows.Chat;

public sealed class ChatOutboxDispatcher : BackgroundService
{
    public const int DefaultPerSecond = 5;

    private static readonly TimeSpan DispatchInterval = TimeSpan.FromSeconds(1);

    private readonly IChatOutbox _chatOutbox;
    private readonly IReadOnlyList<IPlatformChatSender> _chatSenders;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatOutboxDispatcher> _logger;
    private readonly IObservable<SettingChangedEvent> _settingChanges;
    private readonly IWorkflowChatOverlaySink? _overlaySink;
    private readonly WorkflowChatEchoTracker? _echoTracker;

    private IDisposable? _subscription;
    private int _perSecond = DefaultPerSecond;
    private string _outputDestination = WorkflowChatOutputDestination.Dual;
    private bool _initialised;

    public ChatOutboxDispatcher(
        IChatOutbox chatOutbox,
        IEnumerable<IPlatformChatSender> chatSenders,
        IServiceScopeFactory scopeFactory,
        ILogger<ChatOutboxDispatcher> logger,
        IObservable<SettingChangedEvent> settingChanges,
        IWorkflowChatOverlaySink? overlaySink = null,
        WorkflowChatEchoTracker? echoTracker = null)
    {
        _chatOutbox = chatOutbox;
        _chatSenders = chatSenders.ToArray();
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settingChanges = settingChanges;
        _overlaySink = overlaySink;
        _echoTracker = echoTracker;
    }

    public async Task<int> DispatchOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialised)
        {
            await InitialiseAsync(cancellationToken).ConfigureAwait(false);
            _initialised = true;
        }

        var items = await _chatOutbox.DequeuePendingAsync(_perSecond, cancellationToken).ConfigureAwait(false);

        foreach (var item in items)
        {
            var shouldSendToPlatform = WorkflowChatOutputDestination.IncludesPlatform(_outputDestination);
            var shouldSendToOverlay = WorkflowChatOutputDestination.IncludesOverlay(_outputDestination);

            var sender = shouldSendToPlatform
                ? _chatSenders.FirstOrDefault(candidate =>
                    string.Equals(candidate.Platform, item.Platform, StringComparison.OrdinalIgnoreCase))
                : null;

            if (shouldSendToPlatform && sender is null)
            {
                var reason = $"No chat sender registered for platform '{item.Platform}'.";
                _logger.LogWarning("Chat outbox item {ChatOutboxItemId} skipped: {Reason}", item.Id, reason);
                await _chatOutbox.MarkSkippedAsync(item.Id, reason, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                if (sender is not null)
                {
                    await sender.SendAsync(item.Message, cancellationToken).ConfigureAwait(false);
                    _echoTracker?.Track(item.Platform, item.Message);
                }

                await _chatOutbox.MarkSentAsync(item.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Chat outbox item {ChatOutboxItemId} failed.", item.Id);
                await _chatOutbox.MarkFailedAsync(item.Id, ex.Message, cancellationToken).ConfigureAwait(false);
            }
        }

        return items.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchOnceAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(DispatchInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    public override void Dispose()
    {
        _subscription?.Dispose();
        base.Dispose();
    }

    private async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        var value = await settings
            .GetAsync(SystemSettingKey.ChatOutboxPerSecond, DefaultPerSecond, cancellationToken)
            .ConfigureAwait(false);
        ApplyPerSecond(value);
        var outputDestination = await settings
            .GetAsync(SystemSettingKey.WorkflowChatOutputDestination, WorkflowChatOutputDestination.Dual, cancellationToken)
            .ConfigureAwait(false);
        ApplyOutputDestination(outputDestination);
        _subscription = _settingChanges.Subscribe(new SettingObserver(this));
    }

    private void ApplyPerSecond(int value)
    {
        _perSecond = Math.Clamp(value, 1, 1000);
    }

    private void ApplyPerSecond(string? rawJson)
    {
        if (string.IsNullOrEmpty(rawJson))
        {
            ApplyPerSecond(DefaultPerSecond);
            return;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<int>(rawJson);
            ApplyPerSecond(parsed);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse {Key} value {Raw}; falling back to current rate.",
                SystemSettingKey.ChatOutboxPerSecond,
                rawJson);
        }
    }

    private void ApplyOutputDestination(string? value)
    {
        _outputDestination = WorkflowChatOutputDestination.Normalize(value);
    }

    private void ApplyOutputDestinationFromJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            ApplyOutputDestination(WorkflowChatOutputDestination.Dual);
            return;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<string>(rawJson);
            ApplyOutputDestination(parsed);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse {Key} value {Raw}; falling back to current chat destination.",
                SystemSettingKey.WorkflowChatOutputDestination,
                rawJson);
        }
    }

    private sealed class SettingObserver(ChatOutboxDispatcher dispatcher) : IObserver<SettingChangedEvent>
    {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(SettingChangedEvent value)
        {
            if (string.Equals(value.Key, SystemSettingKey.ChatOutboxPerSecond, StringComparison.OrdinalIgnoreCase))
            {
                dispatcher.ApplyPerSecond(value.NewValue);
                return;
            }

            if (!string.Equals(value.Key, SystemSettingKey.WorkflowChatOutputDestination, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            dispatcher.ApplyOutputDestinationFromJson(value.NewValue);
        }
    }
}

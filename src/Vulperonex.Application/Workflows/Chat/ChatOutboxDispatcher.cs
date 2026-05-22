using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Workflows.Actions;

namespace Vulperonex.Application.Workflows.Chat;

public sealed class ChatOutboxDispatcher(
    IChatOutbox chatOutbox,
    IEnumerable<IPlatformChatSender> chatSenders,
    IServiceScopeFactory scopeFactory,
    ILogger<ChatOutboxDispatcher> logger) : BackgroundService
{
    public const int DefaultPerSecond = 5;

    private static readonly TimeSpan DispatchInterval = TimeSpan.FromSeconds(1);
    private readonly IReadOnlyList<IPlatformChatSender> _chatSenders = chatSenders.ToArray();

    public async Task<int> DispatchOnceAsync(CancellationToken cancellationToken = default)
    {
        var perSecond = await GetPerSecondAsync(cancellationToken).ConfigureAwait(false);
        var items = await chatOutbox.DequeuePendingAsync(perSecond, cancellationToken).ConfigureAwait(false);

        foreach (var item in items)
        {
            var sender = _chatSenders.FirstOrDefault(candidate =>
                string.Equals(candidate.Platform, item.Platform, StringComparison.OrdinalIgnoreCase));
            if (sender is null)
            {
                var reason = $"No chat sender registered for platform '{item.Platform}'.";
                logger.LogWarning("Chat outbox item {ChatOutboxItemId} skipped: {Reason}", item.Id, reason);
                await chatOutbox.MarkSkippedAsync(item.Id, reason, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                await sender.SendAsync(item.Message, cancellationToken).ConfigureAwait(false);
                await chatOutbox.MarkSentAsync(item.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Chat outbox item {ChatOutboxItemId} failed.", item.Id);
                await chatOutbox.MarkFailedAsync(item.Id, ex.Message, cancellationToken).ConfigureAwait(false);
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

    private async Task<int> GetPerSecondAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        var value = await settings
            .GetAsync(SystemSettingKey.ChatOutboxPerSecond, DefaultPerSecond, cancellationToken)
            .ConfigureAwait(false);
        return Math.Clamp(value, 1, 1000);
    }
}

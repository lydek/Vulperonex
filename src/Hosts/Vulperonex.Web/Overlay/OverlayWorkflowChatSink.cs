using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Time;
using Vulperonex.Application.Workflows.Chat;
using Vulperonex.Web.SignalR;

namespace Vulperonex.Web.Overlay;

public sealed class OverlayWorkflowChatSink(
    IHubContext<OverlayChatHub> chatHub,
    IOverlayHistoryService<OverlayChatPayload> chatHistory,
    IServiceScopeFactory scopeFactory,
    IClock clock) : IWorkflowChatOverlaySink
{
    private const string DefaultAssistantDisplayName = "系統小精靈";
    private const string DefaultCheckInDisplayName = "打卡系統";

    public async Task PublishAssistantMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        var displayName = await settings
            .GetAsync(SystemSettingKey.OverlayChatAssistantDisplayName, string.Empty, cancellationToken)
            .ConfigureAwait(false);
        var avatarUrl = await settings
            .GetAsync<string?>(SystemSettingKey.OverlayChatAssistantAvatarUrl, null, cancellationToken)
            .ConfigureAwait(false);

        var payload = new OverlayChatPayload(
            1,
            Guid.NewGuid().ToString("N"),
            clock.UtcNow,
            string.IsNullOrWhiteSpace(displayName) ? DefaultAssistantDisplayName : displayName.Trim(),
            "#8a6cff",
            [new OverlayTextSegment("text", message)],
            [],
            AvatarUrl: string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim(),
            Variant: "assistant");

        await chatHistory.AddAsync(payload, cancellationToken).ConfigureAwait(false);
        await chatHub.Clients.All.SendAsync("event", payload, cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishCheckInCardAsync(
        WorkflowCheckInCardOverlayMessage message,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        var displayName = await settings
            .GetAsync(SystemSettingKey.OverlayChatCheckInDisplayName, DefaultCheckInDisplayName, cancellationToken)
            .ConfigureAwait(false);

        var payload = new OverlayChatPayload(
            1,
            Guid.NewGuid().ToString("N"),
            clock.UtcNow,
            string.IsNullOrWhiteSpace(displayName) ? DefaultCheckInDisplayName : displayName.Trim(),
            "#ffd700",
            [],
            [],
            AvatarUrl: message.AvatarUrl,
            MemberSnapshot: new OverlayMemberSnapshot(
                message.DisplayName,
                message.AvatarUrl,
                message.CheckInCount),
            Variant: "checkin-card");

        await chatHistory.AddAsync(payload, cancellationToken).ConfigureAwait(false);
        await chatHub.Clients.All.SendAsync("event", payload, cancellationToken).ConfigureAwait(false);
    }
}

using Microsoft.AspNetCore.SignalR;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Web.SignalR;

namespace Vulperonex.Web.Endpoints;

public static class OverlayHistoryEndpoints
{
    public static IEndpointRouteBuilder MapOverlayHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/api/overlay/chat/messages", async (
            IOverlayHistoryService<OverlayChatPayload> history,
            IHubContext<OverlayChatHub> hub,
            CancellationToken cancellationToken) =>
        {
            await history.ClearAllAsync(cancellationToken);
            await hub.Clients.All.SendAsync("cleared", new { hubName = "chat" }, cancellationToken);
            return Results.NoContent();
        });

        endpoints.MapDelete("/api/overlay/alerts/messages", async (
            IOverlayHistoryService<OverlayAlertPayload> history,
            IHubContext<OverlayAlertsHub> hub,
            CancellationToken cancellationToken) =>
        {
            await history.ClearAllAsync(cancellationToken);
            await hub.Clients.All.SendAsync("cleared", new { hubName = "alerts" }, cancellationToken);
            return Results.NoContent();
        });

        endpoints.MapDelete("/api/overlay/member/messages", async (
            IOverlayHistoryService<OverlayMemberPayload> history,
            IHubContext<OverlayMemberHub> hub,
            CancellationToken cancellationToken) =>
        {
            await history.ClearAllAsync(cancellationToken);
            await hub.Clients.All.SendAsync("cleared", new { hubName = "member" }, cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }
}

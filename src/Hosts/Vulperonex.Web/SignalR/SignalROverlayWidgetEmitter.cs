using Microsoft.AspNetCore.SignalR;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;

namespace Vulperonex.Web.SignalR;

public sealed class SignalROverlayWidgetEmitter(
    IHubContext<OverlayWidgetsHub> hub,
    IOverlayHistoryService<OverlayWidgetPayload> history) : IOverlayWidgetEmitter
{
    public async Task EmitAsync(OverlayWidgetPayload payload, CancellationToken cancellationToken = default)
    {
        await history.AddAsync(payload, cancellationToken);
        await hub.Clients.All.SendAsync("event", payload, cancellationToken);
    }
}

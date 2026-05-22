using Microsoft.AspNetCore.SignalR;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;

namespace Vulperonex.Web.SignalR;

public sealed class SignalROverlayEffectEmitter(
    IHubContext<OverlayEffectsHub> hub) : IOverlayEffectEmitter
{
    public Task EmitAsync(OverlayEffectPayload payload, CancellationToken cancellationToken = default)
    {
        return hub.Clients.All.SendAsync("event", payload, cancellationToken);
    }
}

using Vulperonex.Application.Overlay.Dtos;

namespace Vulperonex.Application.Overlay;

public interface IOverlayWidgetEmitter
{
    Task EmitAsync(OverlayWidgetPayload payload, CancellationToken cancellationToken = default);
}

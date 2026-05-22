using Vulperonex.Application.Overlay.Dtos;

namespace Vulperonex.Application.Overlay;

public interface IOverlayEffectEmitter
{
    Task EmitAsync(OverlayEffectPayload payload, CancellationToken cancellationToken = default);
}

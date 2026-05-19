using Vulperonex.Adapters.Abstractions;
using Vulperonex.Domain.Events;

namespace Vulperonex.Adapters.Simulation;

public interface ISimulationAdapter : IStreamEventSource
{
    Task<IStreamEvent> SimulateAsync(SimulationRequest request, CancellationToken cancellationToken = default);
}

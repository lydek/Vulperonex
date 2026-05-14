using Vulperonex.Adapters.Abstractions;

namespace Vulperonex.Adapters.Simulation;

public interface ISimulationAdapter : IStreamEventSource
{
    Task SimulateAsync(SimulationRequest request, CancellationToken cancellationToken = default);
}

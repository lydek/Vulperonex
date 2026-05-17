namespace Vulperonex.Web.Ports;

public sealed class PortPairAllocator(IPortAvailabilityProbe probe, PortAllocationOptions? options = null)
{
    private readonly PortAllocationOptions _options = options ?? new PortAllocationOptions();

    public PortPair? TryAllocate()
    {
        for (var apiPort = _options.FirstApiPort; apiPort <= _options.LastApiPort; apiPort += _options.PortStep)
        {
            var overlayPort = apiPort + 1;
            if (probe.IsAvailable(apiPort) && probe.IsAvailable(overlayPort))
            {
                return new PortPair(apiPort, overlayPort);
            }
        }

        return null;
    }
}

namespace Vulperonex.Web.Ports;

public sealed class PortPairAllocator(IPortAvailabilityProbe probe)
{
    public PortPair? TryAllocate()
    {
        for (var apiPort = 5000; apiPort <= 5008; apiPort += 2)
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

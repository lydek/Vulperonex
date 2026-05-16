namespace Vulperonex.Web.Ports;

public interface IPortAvailabilityProbe
{
    bool IsAvailable(int port);
}

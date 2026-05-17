using System.Net;
using System.Net.Sockets;

namespace Vulperonex.Web.Ports;

public sealed class SocketPortAvailabilityProbe : IPortAvailabilityProbe
{
    public bool IsAvailable(int port)
    {
        try
        {
            using var ipv4 = new TcpListener(IPAddress.Loopback, port);
            using var ipv6 = new TcpListener(IPAddress.IPv6Loopback, port);
            ipv4.Start();
            ipv6.Start();
            // This is only an availability probe. Stop immediately so Kestrel can bind next;
            // the inherent probe-then-bind TOCTOU window is handled by startup failure.
            ipv6.Stop();
            ipv4.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}

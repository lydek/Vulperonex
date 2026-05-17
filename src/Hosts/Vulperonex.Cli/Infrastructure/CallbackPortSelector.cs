using System.Net;
using System.Net.Sockets;

internal static class CallbackPortSelector
{
    public static int Select()
    {
        foreach (var port in new[] { 7979, 7980, 7981 })
        {
            if (IsPortAvailable(port))
            {
                return port;
            }
        }

        throw new InvalidOperationException("No OAuth callback port is available.");
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}

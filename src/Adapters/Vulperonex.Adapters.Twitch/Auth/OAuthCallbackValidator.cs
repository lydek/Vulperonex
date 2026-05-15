using System.Net;

namespace Vulperonex.Adapters.Twitch.Auth;

public sealed class OAuthCallbackValidator(int port, PkceStateStore stateStore)
{
    public bool IsValid(OAuthCallbackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return IPAddress.IsLoopback(request.RemoteIpAddress)
            && IsAllowedHost(request.Host)
            && string.Equals(request.Path, "/auth/callback", StringComparison.Ordinal)
            && stateStore.Consume(request.State)
            && !string.IsNullOrWhiteSpace(request.Code);
    }

    private bool IsAllowedHost(string host)
    {
        return string.Equals(host, $"localhost:{port}", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, $"127.0.0.1:{port}", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, $"[::1]:{port}", StringComparison.OrdinalIgnoreCase);
    }
}

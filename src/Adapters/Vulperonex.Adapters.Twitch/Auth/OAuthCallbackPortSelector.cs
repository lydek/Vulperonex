namespace Vulperonex.Adapters.Twitch.Auth;

public sealed class OAuthCallbackPortSelector(Func<int, bool> isAvailable)
{
    private static readonly int[] CandidatePorts = [7979, 7980, 7981];

    public int Select()
    {
        foreach (var port in CandidatePorts)
        {
            if (isAvailable(port))
            {
                return port;
            }
        }

        throw new InvalidOperationException("No OAuth callback port is available. Configure Twitch redirect URIs for ports 7979, 7980, and 7981.");
    }
}

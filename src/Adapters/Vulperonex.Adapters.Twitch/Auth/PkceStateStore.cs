using System.Security.Cryptography;

namespace Vulperonex.Adapters.Twitch.Auth;

public sealed class PkceStateStore(TimeProvider timeProvider)
{
    private readonly Dictionary<string, DateTimeOffset> _states = [];

    public string Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var state = Base64UrlEncode(bytes);
        _states[state] = timeProvider.GetUtcNow();
        return state;
    }

    public bool Consume(string state)
    {
        if (!_states.Remove(state, out var createdAt))
        {
            return false;
        }

        return timeProvider.GetUtcNow() - createdAt <= TimeSpan.FromMinutes(10);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

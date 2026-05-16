using System.Security.Cryptography;

namespace Vulperonex.Adapters.Twitch.Auth;

public sealed class PkceStateStore(TimeProvider timeProvider)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _states = [];

    public string Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var state = Base64UrlEncode(bytes);
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            RemoveExpired(now);
            _states[state] = now;
        }

        return state;
    }

    public bool Consume(string state)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            RemoveExpired(now);

            if (!_states.Remove(state, out var createdAt))
            {
                return false;
            }

            return now - createdAt <= TimeSpan.FromMinutes(10);
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var key in _states.Where(pair => now - pair.Value > TimeSpan.FromMinutes(10)).Select(pair => pair.Key).ToArray())
        {
            _states.Remove(key);
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

using Vulperonex.Adapters.Twitch.Auth;

namespace Vulperonex.Adapters.Twitch.Auth;

public sealed class TwitchOAuthSessionStore(TimeProvider timeProvider)
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(10);
    private readonly object _gate = new();
    private readonly Dictionary<string, TwitchOAuthSession> _sessions = [];

    public TwitchOAuthSession Create(int callbackPort, string? customRedirectUri = null)
    {
        var verifier = PkceCodeChallenge.CreateVerifier();
        var state = new PkceStateStore(timeProvider).Create();
        var redirectUri = !string.IsNullOrWhiteSpace(customRedirectUri)
            ? customRedirectUri
            : $"http://localhost:{callbackPort}/auth/callback";

        var session = new TwitchOAuthSession(
            state,
            verifier,
            PkceCodeChallenge.FromVerifier(verifier),
            redirectUri,
            timeProvider.GetUtcNow());

        lock (_gate)
        {
            RemoveExpired(timeProvider.GetUtcNow());
            _sessions[state] = session;
        }

        return session;
    }

    public bool TryConsume(string state, out TwitchOAuthSession session)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            RemoveExpired(now);
            if (!_sessions.Remove(state, out session!))
            {
                return false;
            }

            return now - session.CreatedAt <= SessionTtl;
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var key in _sessions
            .Where(pair => now - pair.Value.CreatedAt > SessionTtl)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _sessions.Remove(key);
        }
    }
}

public sealed record TwitchOAuthSession(
    string State,
    string CodeVerifier,
    string CodeChallenge,
    string RedirectUri,
    DateTimeOffset CreatedAt);

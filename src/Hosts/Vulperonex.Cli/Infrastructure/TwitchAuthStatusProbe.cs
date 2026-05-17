using System.Net.Http.Json;

internal sealed class TwitchAuthStatusProbe(HttpClient client)
{
    public async Task<TwitchAuthStatusProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync("/api/twitch/auth/status", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return TwitchAuthStatusProbeResult.Failure(response.StatusCode.ToString());
        }

        var status = await response.Content.ReadFromJsonAsync<TwitchAuthStatusResponse>(cancellationToken);
        return status is null
            ? TwitchAuthStatusProbeResult.Failure("INVALID_ACTION_CONFIG")
            : TwitchAuthStatusProbeResult.Success(status.ClientIdConfigured, status.ClientSecretConfigured, status.HasRefreshToken);
    }

    private sealed record TwitchAuthStatusResponse(bool ClientIdConfigured, bool ClientSecretConfigured, bool HasRefreshToken);
}

internal sealed record TwitchAuthStatusProbeResult(
    bool Succeeded,
    bool ClientIdConfigured,
    bool ClientSecretConfigured,
    bool HasRefreshToken,
    string? ErrorCode)
{
    public static TwitchAuthStatusProbeResult Success(bool clientIdConfigured, bool clientSecretConfigured, bool hasRefreshToken)
    {
        return new TwitchAuthStatusProbeResult(true, clientIdConfigured, clientSecretConfigured, hasRefreshToken, null);
    }

    public static TwitchAuthStatusProbeResult Failure(string errorCode)
    {
        return new TwitchAuthStatusProbeResult(false, false, false, false, errorCode);
    }
}

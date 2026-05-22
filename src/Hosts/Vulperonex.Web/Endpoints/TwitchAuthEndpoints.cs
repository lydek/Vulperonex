using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Application.Auth;
using Vulperonex.Web.Errors;
using Vulperonex.Web.SignalR;
using Vulperonex.Web.TwitchAuth;

namespace Vulperonex.Web.Endpoints;

public static class TwitchAuthEndpoints
{
    private static readonly int[] AllowedCallbackPorts = [7979, 7980, 7981];

    public static IEndpointRouteBuilder MapTwitchAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/twitch/auth");

        group.MapGet("/status", async (
            IConfiguration configuration,
            IOAuthTokenStore tokenStore,
            CancellationToken cancellationToken) =>
        {
            var clientIdConfigured = !string.IsNullOrWhiteSpace(configuration["Twitch:ClientId"]);
            var clientSecretConfigured = !string.IsNullOrWhiteSpace(configuration["Twitch:ClientSecret"]);
            var hasRefreshToken = await tokenStore.HasRefreshTokenAsync("twitch", cancellationToken);
            return Results.Ok(new TwitchAuthStatusResponse(clientIdConfigured, clientSecretConfigured, hasRefreshToken));
        });

        group.MapDelete("/token", async (
            IOAuthTokenStore tokenStore,
            PlatformConnectionNotifier notifier,
            CancellationToken cancellationToken) =>
        {
            await tokenStore.ClearRefreshTokenAsync("twitch", cancellationToken);
            await notifier.NotifyAsync("twitch", connected: false, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/start", (
            TwitchAuthStartRequest request,
            IConfiguration configuration,
            TwitchOAuthSessionStore sessions) =>
        {
            var clientId = configuration["Twitch:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return ApiErrors.ToResult(ErrorCodes.TwitchClientIdMissing, StatusCodes.Status400BadRequest);
            }

            var callbackPort = request.CallbackPort ?? AllowedCallbackPorts[0];
            if (!IsAllowedCallbackPort(callbackPort))
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            var session = sessions.Create(callbackPort);
            var scopes = configuration["Twitch:Scopes"] ?? "chat:read chat:edit";
            var authorizeUrl = BuildAuthorizeUrl(clientId, scopes, session);
            return Results.Ok(new TwitchAuthStartResponse(authorizeUrl, session.State, callbackPort));
        });

        group.MapPost("/complete", async (
            TwitchAuthCompleteRequest request,
            TwitchOAuthSessionStore sessions,
            ITwitchTokenEndpoint tokenEndpoint,
            IOAuthTokenStore tokenStore,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.State)
                || string.IsNullOrWhiteSpace(request.Code)
                || !sessions.TryConsume(request.State, out var session))
            {
                return ApiErrors.ToResult(ErrorCodes.TwitchOAuthStateInvalid, StatusCodes.Status400BadRequest);
            }

            TwitchTokenResponse token;
            try
            {
                if (tokenEndpoint is TwitchTokenEndpoint concreteEndpoint)
                {
                    token = await concreteEndpoint.ExchangeCodeAsync(
                        request.Code,
                        session.CodeVerifier,
                        session.RedirectUri,
                        cancellationToken);
                }
                else
                {
                    token = await tokenEndpoint.ExchangeCodeAsync(request.Code, session.CodeVerifier, cancellationToken);
                }
            }
            catch (TwitchClientSecretMissingException)
            {
                return ApiErrors.ToResult(ErrorCodes.TwitchClientSecretMissing, StatusCodes.Status400BadRequest);
            }
            catch (TwitchTokenExchangeException)
            {
                return ApiErrors.ToResult(ErrorCodes.TwitchOAuthExchangeFailed, StatusCodes.Status400BadRequest);
            }

            await tokenStore.StoreRefreshTokenAsync("twitch", token.RefreshToken, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/device/start", async (
            IConfiguration configuration,
            TwitchTokenEndpoint tokenEndpoint,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(configuration["Twitch:ClientId"]))
            {
                return ApiErrors.ToResult(ErrorCodes.TwitchClientIdMissing, StatusCodes.Status400BadRequest);
            }

            try
            {
                var scopes = configuration["Twitch:Scopes"] ?? "chat:read chat:edit";
                var authorization = await tokenEndpoint.StartDeviceAuthorizationAsync(scopes, cancellationToken);
                return Results.Ok(authorization);
            }
            catch (TwitchTokenExchangeException)
            {
                return ApiErrors.ToResult(ErrorCodes.TwitchOAuthExchangeFailed, StatusCodes.Status400BadRequest);
            }
        });

        group.MapPost("/device/complete", async (
            TwitchDeviceCompleteRequest request,
            TwitchTokenEndpoint tokenEndpoint,
            IOAuthTokenStore tokenStore,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var token = await tokenEndpoint.CompleteDeviceAuthorizationAsync(request.DeviceCode, cancellationToken);
                await tokenStore.StoreRefreshTokenAsync("twitch", token.RefreshToken, cancellationToken);
                return Results.NoContent();
            }
            catch (TwitchDeviceAuthorizationPendingException)
            {
                return Results.Accepted();
            }
            catch (TwitchTokenExchangeException)
            {
                return ApiErrors.ToResult(ErrorCodes.TwitchOAuthExchangeFailed, StatusCodes.Status400BadRequest);
            }
        });

        return endpoints;
    }

    private static bool IsAllowedCallbackPort(int port)
    {
        if (AllowedCallbackPorts.Contains(port))
        {
            return true;
        }

        // Allow any user-space loopback port (>= 1024) so the Web UI can
        // request its own Kestrel port as the OAuth callback target. Kestrel
        // binding already enforces loopback-only, so the trust boundary is
        // unchanged.
        return port is >= 1024 and <= 65535;
    }

    private static string BuildAuthorizeUrl(string clientId, string scopes, TwitchOAuthSession session)
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = session.RedirectUri,
            ["scope"] = scopes,
            ["state"] = session.State,
            ["code_challenge"] = session.CodeChallenge,
            ["code_challenge_method"] = "S256",
        };

        return "https://id.twitch.tv/oauth2/authorize?"
            + string.Join("&", query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private sealed record TwitchAuthStartRequest(int? CallbackPort);
    private sealed record TwitchAuthStartResponse(string AuthorizeUrl, string State, int CallbackPort);
    private sealed record TwitchAuthCompleteRequest(string State, string Code);
    private sealed record TwitchDeviceCompleteRequest(string DeviceCode);
    private sealed record TwitchAuthStatusResponse(bool ClientIdConfigured, bool ClientSecretConfigured, bool HasRefreshToken);
}

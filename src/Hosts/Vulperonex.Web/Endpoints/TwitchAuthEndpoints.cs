using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Application.Auth;
using Vulperonex.Web.Errors;
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
            var hasRefreshToken = await tokenStore.HasRefreshTokenAsync("twitch", cancellationToken);
            return Results.Ok(new TwitchAuthStatusResponse(clientIdConfigured, hasRefreshToken));
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
            if (!AllowedCallbackPorts.Contains(callbackPort))
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

            await tokenStore.StoreRefreshTokenAsync("twitch", token.RefreshToken, cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
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
    private sealed record TwitchAuthStatusResponse(bool ClientIdConfigured, bool HasRefreshToken);
}

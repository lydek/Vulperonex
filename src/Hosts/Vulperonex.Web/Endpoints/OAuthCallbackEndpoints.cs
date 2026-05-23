using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Application.Auth;
using Vulperonex.Web.SignalR;
using Vulperonex.Web.TwitchAuth;

namespace Vulperonex.Web.Endpoints;

public static class OAuthCallbackEndpoints
{
    public static IEndpointRouteBuilder MapOAuthCallbackEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var handler = async (
            HttpContext context,
            TwitchOAuthSessionStore sessions,
            ITwitchTokenEndpoint tokenEndpoint,
            IOAuthTokenStore tokenStore,
            PlatformConnectionNotifier notifier,
            CancellationToken cancellationToken) =>
        {
            var state = context.Request.Query["state"].ToString();
            var code = context.Request.Query["code"].ToString();
            if (string.IsNullOrWhiteSpace(state)
                || string.IsNullOrWhiteSpace(code)
                || !sessions.TryConsume(state, out var session))
            {
                return Results.Redirect("/?oauth=state_invalid");
            }

            try
            {
                if (tokenEndpoint is TwitchTokenEndpoint concreteEndpoint)
                {
                    var token = await concreteEndpoint.ExchangeCodeAsync(
                        code,
                        session.CodeVerifier,
                        session.RedirectUri,
                        cancellationToken);
                    await tokenStore.StoreRefreshTokenAsync("twitch", token.RefreshToken, cancellationToken);
                }
                else
                {
                    var token = await tokenEndpoint.ExchangeCodeAsync(code, session.CodeVerifier, cancellationToken);
                    await tokenStore.StoreRefreshTokenAsync("twitch", token.RefreshToken, cancellationToken);
                }
            }
            catch (TwitchClientSecretMissingException)
            {
                return Results.Redirect("/?oauth=client_secret_missing");
            }
            catch (TwitchTokenExchangeException)
            {
                return Results.Redirect("/?oauth=exchange_failed");
            }

            await notifier.NotifyAsync("twitch", connected: true, cancellationToken);
            return Results.Redirect("/?oauth=success");
        };

        endpoints.MapGet("/auth/callback", handler);
        endpoints.MapGet("/api/auth/twitch/callback", handler);

        return endpoints;
    }
}

using Vulperonex.Application.Twitch;

namespace Vulperonex.Web.Endpoints;

public static class TwitchBadgesEndpoints
{
    public static IEndpointRouteBuilder MapTwitchBadgesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/twitch/badges", (ITwitchBadgeCache cache) =>
        {
            var response = new TwitchBadgesListResponse(
                Ready: cache.IsReady,
                Global: cache.ListGlobal(),
                Channel: cache.ListChannel());

            return Results.Ok(response);
        });

        return endpoints;
    }

    private sealed record TwitchBadgesListResponse(
        bool Ready,
        IReadOnlyList<TwitchBadgeDescriptor> Global,
        IReadOnlyList<TwitchBadgeDescriptor> Channel);
}

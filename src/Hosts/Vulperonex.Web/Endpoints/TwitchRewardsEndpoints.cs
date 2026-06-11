using Vulperonex.Adapters.Twitch.Helix;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Web.Endpoints;

public static class TwitchRewardsEndpoints
{
    public static IEndpointRouteBuilder MapTwitchRewardsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/twitch/rewards");

        group.MapGet("/", (ITwitchRewardCache cache) => Results.Ok(BuildResponse(cache)));

        group.MapPost("/refresh", async (
            ITwitchRewardCache cache,
            CancellationToken cancellationToken) =>
        {
            await cache.RefreshAsync(cancellationToken);
            return Results.Ok(BuildResponse(cache));
        });

        return endpoints;
    }

    private static TwitchRewardsResponse BuildResponse(ITwitchRewardCache cache)
    {
        return new TwitchRewardsResponse(
            cache.IsReady,
            cache.LastRefreshedAt,
            cache.List());
    }

    private sealed record TwitchRewardsResponse(
        bool Ready,
        DateTimeOffset? LastRefreshedAt,
        IReadOnlyList<PlatformRewardDescriptor> Rewards);
}

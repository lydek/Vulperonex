using Vulperonex.Adapters.Simulation;
using Vulperonex.Domain;
using Vulperonex.Web.Errors;
using Vulperonex.Web.Simulation;

namespace Vulperonex.Web.Endpoints;

public static class SimulateEndpoints
{
    public static IEndpointRouteBuilder MapSimulateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/simulate/{alias}", async (
            string alias,
            SimulateRequest request,
            SimulationAliasRegistry aliases,
            ISimulationAdapter adapter,
            CancellationToken cancellationToken) =>
        {
            var resolved = aliases.Find(alias);
            if (resolved is null)
            {
                return ApiErrors.ToResult(ErrorCodes.UnknownSimulateEventType, StatusCodes.Status400BadRequest);
            }

            await adapter.StartAsync(cancellationToken);
            await adapter.SimulateAsync(ToSimulationRequest(resolved.Kind, request), cancellationToken);
            return Results.Accepted();
        });

        return endpoints;
    }

    private static SimulationRequest ToSimulationRequest(SimulationKind kind, SimulateRequest request)
    {
        var user = new StreamUser(
            "simulation",
            request.PlatformUserId ?? "sim-user",
            request.DisplayName ?? "Sim User",
            ToRoles(request.Roles));

        return kind switch
        {
            SimulationKind.Message => SimulationRequest.Message("simulation", user, request.Message ?? string.Empty),
            SimulationKind.Followed => SimulationRequest.Followed("simulation", user),
            SimulationKind.Subscribed => SimulationRequest.Subscribed("simulation", user, request.Tier ?? "1000"),
            _ => throw new NotSupportedException($"Unsupported simulate alias kind: {kind}."),
        };
    }

    private static StreamRole ToRoles(IReadOnlyCollection<string>? roles)
    {
        if (roles is null || roles.Count == 0)
        {
            return StreamRole.None;
        }

        var resolved = StreamRole.None;
        foreach (var role in roles)
        {
            if (Enum.TryParse<StreamRole>(role, ignoreCase: true, out var parsed))
            {
                resolved |= parsed;
            }
        }

        return resolved;
    }

    private sealed record SimulateRequest(
        string? PlatformUserId,
        string? DisplayName,
        string[]? Roles = null,
        string? Message = null,
        string? Tier = null);
}

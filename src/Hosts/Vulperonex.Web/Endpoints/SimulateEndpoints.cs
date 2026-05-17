using System.Text.Json;
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

            var simulationRequest = ToSimulationRequest(resolved.Kind, request);
            if (simulationRequest is null)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            await adapter.StartAsync(cancellationToken);
            await adapter.SimulateAsync(simulationRequest, cancellationToken);
            return Results.Accepted();
        });

        return endpoints;
    }

    private static SimulationRequest? ToSimulationRequest(SimulationKind kind, SimulateRequest request)
    {
        var roles = ToRoles(request.Roles);
        if (roles is null)
        {
            return null;
        }

        var user = new StreamUser(
            "simulation",
            request.PlatformUserId ?? "sim-user",
            request.DisplayName ?? "Sim User",
            roles.Value);

        return kind switch
        {
            SimulationKind.Message => SimulationRequest.Message("simulation", user, request.Message ?? string.Empty),
            SimulationKind.Followed => SimulationRequest.Followed("simulation", user),
            SimulationKind.Subscribed => SimulationRequest.Subscribed("simulation", user, request.Tier ?? "1000"),
            _ => throw new NotSupportedException($"Unsupported simulate alias kind: {kind}."),
        };
    }

    private static StreamRole? ToRoles(JsonElement? roles)
    {
        if (roles is null || roles.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return StreamRole.None;
        }

        if (roles.Value.ValueKind == JsonValueKind.Number && roles.Value.TryGetInt32(out var numericRoles))
        {
            return Enum.IsDefined((StreamRole)numericRoles) || IsCompositeRoleValue(numericRoles)
                ? (StreamRole)numericRoles
                : null;
        }

        if (roles.Value.ValueKind == JsonValueKind.String)
        {
            return TryParseRole(roles.Value.GetString(), out var singleRole) ? singleRole : null;
        }

        if (roles.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var resolved = StreamRole.None;
        foreach (var role in roles.Value.EnumerateArray())
        {
            if (role.ValueKind != JsonValueKind.String || !TryParseRole(role.GetString(), out var parsed))
            {
                return null;
            }

            resolved |= parsed;
        }

        return resolved;
    }

    private static bool TryParseRole(string? role, out StreamRole parsed)
    {
        return Enum.TryParse(role, ignoreCase: true, out parsed)
            && Enum.IsDefined(parsed);
    }

    private static bool IsCompositeRoleValue(int numericRoles)
    {
        const int allKnownFlags = (int)(StreamRole.Subscriber | StreamRole.Moderator | StreamRole.Vip | StreamRole.Follower);
        return numericRoles >= 0 && (numericRoles & ~allKnownFlags) == 0;
    }

    private sealed record SimulateRequest(
        string? PlatformUserId,
        string? DisplayName,
        JsonElement? Roles = null,
        string? Message = null,
        string? Tier = null);
}

namespace Vulperonex.Web.Endpoints;

using Vulperonex.Application.EventTypes;
using Vulperonex.Web.Simulation;

public static class EventTypeEndpoints
{
    public static IEndpointRouteBuilder MapEventTypeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/event-types", (
            IStreamEventTypeRegistry registry,
            SimulationAliasRegistry aliases) =>
        {
            var eventTypes = registry.GetAll()
                .Select(metadata => new EventTypeResponse(
                    metadata.Key,
                    metadata.Description,
                    aliases.IsSimulatable(metadata.Key)))
                .OrderBy(metadata => metadata.Key, StringComparer.Ordinal)
                .ToArray();

            return Results.Ok(eventTypes);
        });

        return endpoints;
    }

    private sealed record EventTypeResponse(string Key, string Description, bool IsSimulatable);
}

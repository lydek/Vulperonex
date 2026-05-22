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

    // Note: registry.GetAll() already filters out IsSystemEvent=true entries,
    // so the API surface never exposes platform.connection_changed. The dropdown
    // therefore only needs to badge IsSimulatable; system-event filtering is
    // guaranteed server-side by InMemoryStreamEventTypeRegistry.GetAll().
    private sealed record EventTypeResponse(string Key, string Description, bool IsSimulatable);
}

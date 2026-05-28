using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Web.Endpoints;

public static class MetadataEndpoints
{
    public static IEndpointRouteBuilder MapMetadataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/metadata/triggers", (ITriggerMetadataProvider provider) =>
        {
            var eventTypes = provider.GetAvailableEventTypes();
            var response = eventTypes.Select(et => new
            {
                et.Key,
                et.DisplayName,
                et.Description,
                FilterFields = provider.GetFilterFieldsFor(et.Key),
                ValidVariables = provider.GetValidVariablesFor(et.Key)
            }).ToArray();

            return Results.Ok(response);
        });

        endpoints.MapGet("/api/metadata/actions", (IActionMetadataProvider provider) =>
        {
            var actions = provider.GetAvailableActions();
            return Results.Ok(actions);
        });

        return endpoints;
    }
}

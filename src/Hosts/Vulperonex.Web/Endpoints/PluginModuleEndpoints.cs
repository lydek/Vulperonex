using System.Net;
using Microsoft.AspNetCore.Http;
using Vulperonex.Application.Modules;
using Vulperonex.Web.Errors;

namespace Vulperonex.Web.Endpoints;

public static class PluginModuleEndpoints
{
    public static IEndpointRouteBuilder MapPluginModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/plugins-modules", async (
            HttpContext context,
            IModuleStateService modules,
            CancellationToken cancellationToken) =>
        {
            if (!IsLoopbackRequest(context.Connection.RemoteIpAddress))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var items = await modules.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(items.Select(MapDto));
        });

        endpoints.MapPost("/api/plugins-modules/{name}/toggle", async Task<IResult> (
            string name,
            ToggleModuleRequest request,
            HttpContext context,
            IModuleStateService modules,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await modules
                    .ToggleAsync(name, request.Enabled, "user", cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(new ToggleModuleResponse(
                    MapDto(result.Module),
                    result.ChangedModules.Select(MapDto).ToArray()));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "MODULE_NOT_FOUND" });
            }
            catch (ArgumentException)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidModuleName, StatusCodes.Status400BadRequest);
            }
        });

        return endpoints;
    }

    private static bool IsLoopbackRequest(IPAddress? remoteIpAddress)
    {
        return remoteIpAddress is null || IPAddress.IsLoopback(remoteIpAddress);
    }

    private static PluginModuleDto MapDto(ModuleStateSnapshot item)
    {
        return new PluginModuleDto(
            item.Name,
            item.DisplayName,
            item.Kind,
            item.Enabled,
            item.Dependencies,
            item.Dependents);
    }

    private sealed record ToggleModuleRequest(bool Enabled);

    private sealed record ToggleModuleResponse(
        PluginModuleDto Module,
        IReadOnlyList<PluginModuleDto> ChangedModules);

    private sealed record PluginModuleDto(
        string Name,
        string DisplayName,
        string Kind,
        bool Enabled,
        IReadOnlyList<string> Dependencies,
        IReadOnlyList<string> Dependents);
}

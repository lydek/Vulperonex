using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Vulperonex.Web.Ports;
using Vulperonex.Web.Security;

namespace Vulperonex.Web.Endpoints;

/// <summary>
/// Admin-only (loopback + CSRF) read endpoint exposing the cross-machine overlay LAN settings,
/// so the admin UI can render a copy-paste OBS browser-source URL including the access key.
/// </summary>
public static class OverlayLanEndpoints
{
    public static IEndpointRouteBuilder MapOverlayLanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Under /api/overlay/* → already guarded by AdminGuardMiddleware (loopback + Host + CSRF + Origin).
        endpoints.MapGet("/api/overlay/lan-info", (
            [FromServices] IConfiguration configuration,
            [FromServices] PortPair ports,
            [FromServices] OverlayLanAccessKeyProvider keyProvider) =>
        {
            var enabled = configuration.GetValue("Overlay:Lan:Enabled", false);
            var bindAddress = configuration["Overlay:Lan:BindAddress"] ?? "0.0.0.0";

            return Results.Ok(new OverlayLanInfoResponse(
                enabled,
                bindAddress,
                ports.OverlayPort,
                enabled ? keyProvider.Key : null));
        });

        return endpoints;
    }
}

public sealed record OverlayLanInfoResponse(bool Enabled, string BindAddress, int OverlayPort, string? AccessKey);

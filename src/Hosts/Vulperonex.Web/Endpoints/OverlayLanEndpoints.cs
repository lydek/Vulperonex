using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
                enabled ? keyProvider.Key : null,
                enabled ? GetSuggestedLanHosts(bindAddress) : Array.Empty<string>()));
        });

        return endpoints;
    }

    private static IReadOnlyList<string> GetSuggestedLanHosts(string bindAddress)
    {
        var trimmed = bindAddress.Trim();
        if (IPAddress.TryParse(trimmed, out var configuredAddress)
            && !IPAddress.IsLoopback(configuredAddress)
            && configuredAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            if (!configuredAddress.Equals(IPAddress.Any))
            {
                return [configuredAddress.ToString()];
            }
        }

        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            .Select(address => address.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(address => address, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed record OverlayLanInfoResponse(
    bool Enabled,
    string BindAddress,
    int OverlayPort,
    string? AccessKey,
    IReadOnlyList<string> SuggestedHosts);

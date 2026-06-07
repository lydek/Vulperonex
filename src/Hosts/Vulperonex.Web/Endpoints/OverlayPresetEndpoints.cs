using System.Net;
using Microsoft.AspNetCore.Http.Features;
using Vulperonex.Application.Settings;
using Vulperonex.Web.Errors;
using Vulperonex.Web.Overlay;

namespace Vulperonex.Web.Endpoints;

public static class OverlayPresetEndpoints
{
    public static IEndpointRouteBuilder MapOverlayPresetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/overlay/presets", (HttpContext context, OverlayPresetStore store) =>
        {
            return Results.Ok(store.ListAll());
        });

        // Overlay customization image upload (background / stamp). Images only, validated + size-capped.
        endpoints.MapPost("/api/overlay/assets", async (
            HttpContext context,
            OverlayPresetStore store,
            CancellationToken cancellationToken) =>
        {
            if (!context.Request.HasFormContentType)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();
            if (file is null)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            if (file.Length > OverlayPresetStore.MaxAssetBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            try
            {
                var url = await store.SaveAssetAsync(file, cancellationToken);
                return Results.Ok(new OverlayAssetUploadResponse(url));
            }
            catch (InvalidDataException)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }
        })
        .DisableAntiforgery();

        endpoints.MapGet("/overlay/chat", (
            HttpContext context,
            OverlayPresetStore store,
            ISystemSettingsService settings,
            CancellationToken cancellationToken) =>
            HandleOverlayHubAsync("chat", context, store, settings, cancellationToken));

        endpoints.MapGet("/overlay/member", (
            HttpContext context,
            OverlayPresetStore store,
            ISystemSettingsService settings,
            CancellationToken cancellationToken) =>
            HandleOverlayHubAsync("member", context, store, settings, cancellationToken));

        endpoints.MapGet("/overlay/alerts", (
            HttpContext context,
            OverlayPresetStore store,
            ISystemSettingsService settings,
            CancellationToken cancellationToken) =>
            HandleOverlayHubAsync("alerts", context, store, settings, cancellationToken));

        return endpoints;
    }

    private static async Task<IResult> HandleOverlayHubAsync(
        string hub,
        HttpContext context,
        OverlayPresetStore store,
        ISystemSettingsService settings,
        CancellationToken cancellationToken)
    {
        var queryPreset = context.Request.Query["preset"].ToString();
        var configuredPreset = !string.IsNullOrWhiteSpace(queryPreset)
            ? queryPreset
            : await settings.GetAsync<string?>(store.GetSettingKeyForHub(hub), null, cancellationToken);

        if (TryResolvePresetRelativePath(configuredPreset, hub, store, out var relativePath))
        {
            if (relativePath!.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
                if (!string.IsNullOrWhiteSpace(configuredPreset) &&
                    !queryString.Contains("preset=", StringComparison.OrdinalIgnoreCase))
                {
                    var separator = string.IsNullOrEmpty(queryString) ? "?" : "&";
                    queryString += $"{separator}preset={configuredPreset}";
                }
                return Results.Redirect($"{relativePath}{queryString}");
            }
        }

        if (TryResolveDefaultStaticRelativePath(hub, out var defaultRelativePath))
        {
            var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
            return Results.Redirect($"{defaultRelativePath}{queryString}");
        }

        return ServeSpaIndex(context);
    }

    private static bool TryResolveDefaultStaticRelativePath(string hub, out string? relativePath)
    {
        relativePath = hub switch
        {
            "chat" => "/overlay/chat.html",
            "member" => "/overlay/member-card.html",
            _ => null,
        };

        return relativePath is not null;
    }

    private static bool TryResolvePresetRelativePath(string? configuredPreset, string hub, OverlayPresetStore store, out string? relativePath)
    {
        relativePath = null;
        if (string.IsNullOrWhiteSpace(configuredPreset))
        {
            return false;
        }

        var builtin = store.GetBuiltIns()
            .FirstOrDefault(p => string.Equals(p.HubName, hub, StringComparison.OrdinalIgnoreCase)
                                 && string.Equals(p.Key, configuredPreset, StringComparison.OrdinalIgnoreCase));
        if (builtin is not null)
        {
            relativePath = builtin.RelativeUrl;
            return true;
        }

        return false;
    }

    private static IResult ServeSpaIndex(HttpContext context)
    {
        var environment = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var indexFile = environment.WebRootFileProvider.GetFileInfo("index.html");
        if (!indexFile.Exists || string.IsNullOrWhiteSpace(indexFile.PhysicalPath))
        {
            return Results.NotFound();
        }

        return Results.File(indexFile.PhysicalPath, "text/html; charset=utf-8");
    }

    private sealed record OverlayAssetUploadResponse(string Url);
}

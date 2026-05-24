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
        endpoints.MapPost("/api/overlay/custom-presets", async (
            HttpContext context,
            OverlayPresetStore store,
            CancellationToken cancellationToken) =>
        {
            if (!IsLoopbackRequest(context.Connection.RemoteIpAddress))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!context.Request.HasFormContentType)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var slug = form["slug"].ToString().Trim().ToLowerInvariant();
            if (!store.IsValidSlug(slug))
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            var file = form.Files["file"] ?? form.Files.FirstOrDefault();
            if (file is null)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            if (file.Length > OverlayPresetStore.MaxUploadBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            try
            {
                var metadata = await store.SaveAsync(slug, file, cancellationToken);
                return Results.Created($"/overlay/custom/{slug}/index.html", metadata);
            }
            catch (InvalidDataException)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }
        })
        .DisableAntiforgery();

        endpoints.MapGet("/api/overlay/custom-presets", (OverlayPresetStore store) =>
            Results.Ok(store.ListCustom()));

        endpoints.MapDelete("/api/overlay/custom-presets/{slug}", (string slug, OverlayPresetStore store) =>
        {
            if (!store.IsValidSlug(slug))
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            store.Delete(slug);
            return Results.NoContent();
        });

        endpoints.MapGet("/api/overlay/presets", (OverlayPresetStore store) =>
            Results.Ok(store.ListAll()));

        endpoints.MapGet("/overlay/{hub}", async (
            string hub,
            HttpContext context,
            OverlayPresetStore store,
            ISystemSettingsService settings,
            CancellationToken cancellationToken) =>
        {
            if (!store.IsSupportedHub(hub))
            {
                return Results.NotFound();
            }

            var queryPreset = context.Request.Query["preset"].ToString();
            var configuredPreset = !string.IsNullOrWhiteSpace(queryPreset)
                ? queryPreset
                : await settings.GetAsync<string?>(store.GetSettingKeyForHub(hub), null, cancellationToken);

            if (TryResolveCustomPreset(configuredPreset, store, out var relativePath))
            {
                var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
                return Results.Redirect($"{relativePath}{queryString}");
            }

            return ServeSpaIndex(context);
        });

        return endpoints;
    }

    private static bool TryResolveCustomPreset(string? configuredPreset, OverlayPresetStore store, out string? relativePath)
    {
        relativePath = null;
        if (string.IsNullOrWhiteSpace(configuredPreset) || !configuredPreset.StartsWith("custom:", StringComparison.Ordinal))
        {
            return false;
        }

        var slug = configuredPreset["custom:".Length..];
        relativePath = store.ResolveCustomRelativePath(slug);
        return relativePath is not null;
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

    private static bool IsLoopbackRequest(IPAddress? remoteIpAddress)
    {
        return remoteIpAddress is null || IPAddress.IsLoopback(remoteIpAddress);
    }
}

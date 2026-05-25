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
            catch (InvalidOperationException ex) when (ex.Message.Contains("Another operation is already in progress"))
            {
                return ApiErrors.ToResult(ErrorCodes.PresetLocked, StatusCodes.Status409Conflict);
            }
        })
        .DisableAntiforgery();

        endpoints.MapGet("/api/overlay/custom-presets", (HttpContext context, OverlayPresetStore store) =>
        {
            return Results.Ok(store.ListCustom());
        });

        endpoints.MapDelete("/api/overlay/custom-presets/{slug}", async (
            HttpContext context,
            string slug,
            OverlayPresetStore store,
            CancellationToken cancellationToken) =>
        {
            if (!store.IsValidSlug(slug))
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            try
            {
                var deleted = await store.DeleteAsync(slug, cancellationToken);
                if (!deleted)
                {
                    return ApiErrors.ToResult(ErrorCodes.PresetNotFound, StatusCodes.Status404NotFound);
                }
                return Results.NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Another operation is already in progress"))
            {
                return ApiErrors.ToResult(ErrorCodes.PresetLocked, StatusCodes.Status409Conflict);
            }
        });

        endpoints.MapGet("/api/overlay/presets", (HttpContext context, OverlayPresetStore store) =>
        {
            return Results.Ok(store.ListAll());
        });

        endpoints.MapGet("/api/overlay/custom-presets/{slug}/files", async (
            HttpContext context,
            string slug,
            OverlayPresetStore store) =>
        {
            if (!store.IsValidSlug(slug)) return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            return Results.Ok(await store.ListDraftFilesAsync(slug));
        });

        endpoints.MapGet("/api/overlay/custom-presets/{slug}/files/{*path}", async (
            HttpContext context,
            string slug,
            string path,
            OverlayPresetStore store) =>
        {
            if (!store.IsValidSlug(slug)) return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            try
            {
                var content = await store.ReadDraftFileAsync(slug, path);
                return Results.Text(content, "text/plain; charset=utf-8");
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidFilePath, StatusCodes.Status400BadRequest);
            }
        });

        endpoints.MapPut("/api/overlay/custom-presets/{slug}/files/{*path}", async (
            HttpContext context,
            string slug,
            string path,
            WriteFileRequest request,
            OverlayPresetStore store) =>
        {
            if (!store.IsValidSlug(slug)) return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);

            // Extension validation
            var extension = Path.GetExtension(path).ToLowerInvariant();
            string[] allowedExtensions = [".html", ".htm", ".css", ".js", ".json", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".woff2"];
            if (!allowedExtensions.Contains(extension) || path.EndsWith("web.config", StringComparison.OrdinalIgnoreCase))
            {
                return ApiErrors.ToResult(ErrorCodes.UnsupportedFileExtension, StatusCodes.Status400BadRequest);
            }

            try
            {
                await store.WriteDraftFileAsync(slug, path, request.Content ?? string.Empty);
                return Results.NoContent();
            }
            catch (InvalidDataException)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Another operation is already in progress"))
            {
                return ApiErrors.ToResult(ErrorCodes.PresetLocked, StatusCodes.Status409Conflict);
            }
            catch (InvalidOperationException)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidFilePath, StatusCodes.Status400BadRequest);
            }
        });

        endpoints.MapDelete("/api/overlay/custom-presets/{slug}/files/{*path}", async (
            HttpContext context,
            string slug,
            string path,
            OverlayPresetStore store) =>
        {
            if (!store.IsValidSlug(slug)) return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            try
            {
                await store.DeleteDraftFileAsync(slug, path);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Another operation is already in progress"))
            {
                return ApiErrors.ToResult(ErrorCodes.PresetLocked, StatusCodes.Status409Conflict);
            }
            catch (InvalidOperationException)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidFilePath, StatusCodes.Status400BadRequest);
            }
        });

        endpoints.MapPost("/api/overlay/custom-presets/{slug}/deploy", async (
            HttpContext context,
            string slug,
            OverlayPresetStore store) =>
        {
            if (!store.IsValidSlug(slug)) return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            try
            {
                await store.DeployDraftAsync(slug);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Another operation is already in progress"))
            {
                return ApiErrors.ToResult(ErrorCodes.PresetLocked, StatusCodes.Status409Conflict);
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.StartsWith("Draft validation failed", StringComparison.Ordinal))
                {
                    return ApiErrors.ToResult(ErrorCodes.DraftValidationFailed, StatusCodes.Status400BadRequest);
                }
                return ApiErrors.ToResult(ErrorCodes.DeployFailed, StatusCodes.Status400BadRequest);
            }
        });

        endpoints.MapPost("/api/overlay/custom-presets/{slug}/validate", async (
            HttpContext context,
            string slug,
            OverlayPresetStore store) =>
        {
            if (!store.IsValidSlug(slug)) return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            return Results.Ok(await store.ValidateDraftAsync(slug));
        });

        endpoints.MapGet("/api/overlay/custom-presets/{slug}/history", async (
            HttpContext context,
            string slug,
            OverlayPresetStore store) =>
        {
            if (!store.IsValidSlug(slug)) return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            return Results.Ok(await store.ListHistoryVersionsAsync(slug));
        });

        endpoints.MapPost("/api/overlay/custom-presets/{slug}/rollback/{versionStamp}", async (
            HttpContext context,
            string slug,
            string versionStamp,
            OverlayPresetStore store) =>
        {
            if (!store.IsValidSlug(slug)) return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            try
            {
                await store.RollbackToVersionAsync(slug, versionStamp);
                return Results.NoContent();
            }
            catch (FileNotFoundException)
            {
                return ApiErrors.ToResult(ErrorCodes.VersionNotFound, StatusCodes.Status404NotFound);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Another operation is already in progress"))
            {
                return ApiErrors.ToResult(ErrorCodes.PresetLocked, StatusCodes.Status409Conflict);
            }
        });

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

    private sealed record WriteFileRequest(string? Content);
}

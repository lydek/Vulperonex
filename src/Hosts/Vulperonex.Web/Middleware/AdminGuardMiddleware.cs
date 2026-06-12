using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Vulperonex.Application.Settings;
using Vulperonex.Web.Errors;
using Vulperonex.Web.Security;

namespace Vulperonex.Web.Middleware;

public sealed class AdminGuardMiddleware
{
    private readonly RequestDelegate _next;

    // Config keys the live overlay legitimately reads. Anything else (e.g. twitch.client_id)
    // must never be reachable from a LAN address.
    private static readonly HashSet<string> OverlaySafeConfigKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        SystemSettingKey.OverlayChatPreset,
        SystemSettingKey.OverlayMemberPreset,
        SystemSettingKey.OverlayAlertsPreset,
        SystemSettingKey.OverlayChatShowMemberCard,
        SystemSettingKey.OverlayMemberBackgroundUrl,
        SystemSettingKey.OverlayMemberStampUrl,
    };

    public AdminGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        AdminCsrfTokenProvider tokenProvider,
        OverlayLanAccessKeyProvider overlayKeyProvider)
    {
        var path = context.Request.Path.Value;
        var method = context.Request.Method;

        // Non-loopback (LAN) requests are confined to the live overlay surface and gated by the
        // overlay access key. Everything else (admin SPA APIs, mutating verbs, OAuth, editor) is
        // rejected before the loopback admin checks below ever run.
        if (!IsLoopbackRequest(context.Connection.RemoteIpAddress))
        {
            var lanCheck = ValidateLanOverlayRequest(context, overlayKeyProvider, path, method);
            if (lanCheck is not null)
            {
                await lanCheck.ExecuteAsync(context).ConfigureAwait(false);
                return;
            }

            await _next(context).ConfigureAwait(false);
            return;
        }

        if (path != null)
        {
            // URL normalization to prevent bypass: decode %2F / %2f and compress multiple slashes //
            var normalizedPath = NormalizePath(path);

            // Allow bypass for /api/overlay/csrf-token (performs loopback and Host checks only, waives CSRF validation)
            if (string.Equals(normalizedPath, "/api/overlay/csrf-token", StringComparison.OrdinalIgnoreCase))
            {
                var loopbackCheck = ValidateLoopbackAndHostOnly(context);
                if (loopbackCheck is not null)
                {
                    await loopbackCheck.ExecuteAsync(context).ConfigureAwait(false);
                    return;
                }
            }
            else
            {
                // Any endpoint that starts with /api/* and is not GET (i.e. mutating verbs)
                // Or any endpoint that starts with /api/overlay/*
                bool shouldProtect = (normalizedPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) && !HttpMethods.IsGet(method))
                                     || normalizedPath.StartsWith("/api/overlay/", StringComparison.OrdinalIgnoreCase);

                if (shouldProtect)
                {
                    var authCheck = ValidateAdminRequest(context, tokenProvider);
                    if (authCheck is not null)
                    {
                        await authCheck.ExecuteAsync(context).ConfigureAwait(false);
                        return;
                    }
                }
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    private static string NormalizePath(string path)
    {
        var normalizedPath = path.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase)
                                 .Replace("%2f", "/", StringComparison.OrdinalIgnoreCase);

        while (normalizedPath.Contains("//", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalizedPath;
    }

    /// <summary>
    /// Gates a non-loopback (LAN) request. Returns <c>null</c> to allow the request through,
    /// or an <see cref="IResult"/> (403) to short-circuit it.
    /// Allowed: static SPA/overlay assets (GET, no key — they are public client code and cannot
    /// carry headers as sub-resource loads); SignalR hubs and overlay-safe config GETs (key required).
    /// Rejected: all other backend paths (admin APIs, mutating verbs, OAuth, editor, health, openapi).
    /// </summary>
    private static IResult? ValidateLanOverlayRequest(
        HttpContext context,
        OverlayLanAccessKeyProvider keyProvider,
        string? path,
        string method)
    {
        if (path is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var normalizedPath = NormalizePath(path);

        var isHub = normalizedPath.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase);
        var isApi = normalizedPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedPath, "/api", StringComparison.OrdinalIgnoreCase);
        var isOtherBackend = normalizedPath.StartsWith("/auth", StringComparison.OrdinalIgnoreCase)
                             || normalizedPath.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(normalizedPath, "/health", StringComparison.OrdinalIgnoreCase);

        // Static SPA / overlay HTML / assets: public, GET only, no key (sub-resource loads cannot attach a key).
        if (!isHub && !isApi && !isOtherBackend)
        {
            return HttpMethods.IsGet(method) ? null : Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        // SignalR hubs: live event stream — key required.
        if (isHub)
        {
            return ValidateOverlayKey(context, keyProvider);
        }

        // Overlay-safe config reads only.
        if (isApi
            && HttpMethods.IsGet(method)
            && normalizedPath.StartsWith("/api/config/", StringComparison.OrdinalIgnoreCase))
        {
            var key = normalizedPath["/api/config/".Length..];
            if (OverlaySafeConfigKeys.Contains(key))
            {
                return ValidateOverlayKey(context, keyProvider);
            }
        }

        // Everything else backend (admin/mutating/OAuth/editor/health/openapi) is forbidden over LAN.
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static IResult? ValidateOverlayKey(HttpContext context, OverlayLanAccessKeyProvider keyProvider)
    {
        var candidate = context.Request.Query["k"].ToString();
        if (string.IsNullOrEmpty(candidate))
        {
            candidate = context.Request.Headers["X-Overlay-Key"].ToString();
        }

        return keyProvider.Validate(candidate)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static bool IsLoopbackRequest(IPAddress? remoteIpAddress)
    {
        // Strictly reject remoteIpAddress being null to prevent Unix socket/Proxy infiltration
        return remoteIpAddress is not null && IPAddress.IsLoopback(remoteIpAddress);
    }

    private static bool IsAllowedHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;

        // Remove port section
        var hostName = host;
        var portIndex = host.IndexOf(':');
        if (portIndex >= 0)
        {
            hostName = host.Substring(0, portIndex);
        }

        return string.Equals(hostName, "localhost", StringComparison.OrdinalIgnoreCase)
               || string.Equals(hostName, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(hostName, "[::1]", StringComparison.OrdinalIgnoreCase);
    }

    private static IResult? ValidateLoopbackAndHostOnly(HttpContext context)
    {
        if (!IsLoopbackRequest(context.Connection.RemoteIpAddress))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var hostHeader = context.Request.Host.Value;
        if (!IsAllowedHost(hostHeader))
        {
            return ApiErrors.ToResult(ErrorCodes.OriginMismatch, StatusCodes.Status400BadRequest);
        }

        return null;
    }

    private static IResult? ValidateAdminRequest(HttpContext context, AdminCsrfTokenProvider tokenProvider)
    {
        if (!IsLoopbackRequest(context.Connection.RemoteIpAddress))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        // Host allowlist check: prevent DNS Rebinding
        var hostHeader = context.Request.Host.Value;
        if (!IsAllowedHost(hostHeader))
        {
            return ApiErrors.ToResult(ErrorCodes.OriginMismatch, StatusCodes.Status400BadRequest);
        }

        // Constant-time comparison for token verification
        var csrfHeader = context.Request.Headers["X-Admin-Csrf"].ToString();
        if (string.IsNullOrEmpty(csrfHeader) || !CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(csrfHeader),
            System.Text.Encoding.UTF8.GetBytes(tokenProvider.Token)))
        {
            return ApiErrors.ToResult(ErrorCodes.MissingOrInvalidCsrfHeader, StatusCodes.Status400BadRequest);
        }

        var origin = context.Request.Headers["Origin"].ToString();
        var referer = context.Request.Headers["Referer"].ToString();

        if (string.IsNullOrEmpty(origin) && string.IsNullOrEmpty(referer))
        {
            return ApiErrors.ToResult(ErrorCodes.MissingOriginOrRefererHeader, StatusCodes.Status400BadRequest);
        }

        if (!string.IsNullOrEmpty(origin))
        {
            if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
            {
                if (!IsAllowedHost(originUri.Authority) || !string.Equals(originUri.Authority, hostHeader, StringComparison.OrdinalIgnoreCase))
                {
                    return ApiErrors.ToResult(ErrorCodes.OriginMismatch, StatusCodes.Status400BadRequest);
                }
            }
            else
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidOriginHeader, StatusCodes.Status400BadRequest);
            }
        }

        if (!string.IsNullOrEmpty(referer))
        {
            if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            {
                if (!IsAllowedHost(refererUri.Authority) || !string.Equals(refererUri.Authority, hostHeader, StringComparison.OrdinalIgnoreCase))
                {
                    return ApiErrors.ToResult(ErrorCodes.RefererMismatch, StatusCodes.Status400BadRequest);
                }
            }
            else
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidRefererHeader, StatusCodes.Status400BadRequest);
            }
        }

        return null;
    }
}

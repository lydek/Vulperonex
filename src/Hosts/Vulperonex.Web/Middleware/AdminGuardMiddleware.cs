using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Vulperonex.Web.Errors;
using Vulperonex.Web.Security;

namespace Vulperonex.Web.Middleware;

public sealed class AdminGuardMiddleware
{
    private readonly RequestDelegate _next;

    public AdminGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AdminCsrfTokenProvider tokenProvider)
    {
        var path = context.Request.Path.Value;
        var method = context.Request.Method;

        if (path != null)
        {
            // URL 正規化防繞過：解碼 %2F / %2f 並壓縮多重斜線 //
            var normalizedPath = path.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase)
                                     .Replace("%2f", "/", StringComparison.OrdinalIgnoreCase);

            while (normalizedPath.Contains("//", StringComparison.Ordinal))
            {
                normalizedPath = normalizedPath.Replace("//", "/", StringComparison.Ordinal);
            }

            // 對 /api/overlay/csrf-token 放行（僅作 loopback 和 Host 檢查，免除 CSRF 檢驗）
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
                // 只要是 /api/* 且非 GET (即 mutating verbs)
                // 或者是 /api/overlay/* 的所有端點
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

    private static bool IsLoopbackRequest(IPAddress? remoteIpAddress)
    {
        // 嚴格拒絕 remoteIpAddress 為 null，全防 Unix socket/Proxy 滲透
        return remoteIpAddress is not null && IPAddress.IsLoopback(remoteIpAddress);
    }

    private static bool IsAllowedHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;

        // 移除埠部分
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

        // Host 允許清單檢測：防範 DNS Rebinding
        var hostHeader = context.Request.Host.Value;
        if (!IsAllowedHost(hostHeader))
        {
            return ApiErrors.ToResult(ErrorCodes.OriginMismatch, StatusCodes.Status400BadRequest);
        }

        // Token 恆定時間比對
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

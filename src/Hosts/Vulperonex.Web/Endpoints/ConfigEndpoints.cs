using Vulperonex.Application.Settings;
using Vulperonex.Web.Errors;

namespace Vulperonex.Web.Endpoints;

public static class ConfigEndpoints
{
    private static readonly HashSet<string> RegisteredKeys =
    [
        SystemSettingKey.StreamingPlatform,
        SystemSettingKey.BusChannelCapacity,
        SystemSettingKey.OverlayDisplayCacheL1Capacity,
        SystemSettingKey.OverlayDisplayCacheTtlHours,
        SystemSettingKey.LogMinLevel,
        SystemSettingKey.LogDbRetentionDays,
        SystemSettingKey.LogDbMaxSizeMb,
        SystemSettingKey.LogFileRetentionDays,
    ];

    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/config");

        group.MapGet("/{key}", async (string key, ISystemSettingsService settings, CancellationToken cancellationToken) =>
        {
            var protectedResult = ValidateKey(key);
            if (protectedResult is not null)
            {
                return protectedResult;
            }

            var value = await settings.GetAsync<string?>(key, null, cancellationToken);
            return Results.Ok(new ConfigValueResponse(key.ToLowerInvariant(), value));
        });

        group.MapPut("/{key}", async (
            string key,
            ConfigValueRequest request,
            ISystemSettingsService settings,
            CancellationToken cancellationToken) =>
        {
            var protectedResult = ValidateKey(key);
            if (protectedResult is not null)
            {
                return protectedResult;
            }

            await settings.SetAsync(key, request.Value, "user", cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static IResult? ValidateKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.StartsWith("security.", StringComparison.Ordinal))
        {
            return ApiErrors.ToResult(ErrorCodes.ConfigKeySecurityNamespace, StatusCodes.Status403Forbidden);
        }

        if (normalized.StartsWith("oauth.", StringComparison.Ordinal))
        {
            return ApiErrors.ToResult(ErrorCodes.OAuthCredentialNamespace, StatusCodes.Status403Forbidden);
        }

        return RegisteredKeys.Contains(normalized)
            ? null
            : ApiErrors.ToResult(ErrorCodes.UnknownConfigKey, StatusCodes.Status400BadRequest);
    }

    private sealed record ConfigValueRequest(string Value);
    private sealed record ConfigValueResponse(string Key, string? Value);
}

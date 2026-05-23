using Vulperonex.Application.Settings;
using Vulperonex.Web.Errors;

namespace Vulperonex.Web.Endpoints;

public static class ConfigEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> RegisteredKeys = new[]
    {
        SystemSettingKey.StreamingPlatform,
        SystemSettingKey.BusChannelCapacity,
        SystemSettingKey.OverlayDisplayCacheL1Capacity,
        SystemSettingKey.OverlayDisplayCacheTtlHours,
        SystemSettingKey.LogMinLevel,
        SystemSettingKey.LogDbRetentionDays,
        SystemSettingKey.LogDbMaxSizeMb,
        SystemSettingKey.LogFileRetentionDays,
        SystemSettingKey.OverlayChatPreset,
    }.ToDictionary(key => key.ToLowerInvariant(), key => key, StringComparer.Ordinal);

    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/config");

        group.MapGet("/{key}", async (string key, ISystemSettingsService settings, CancellationToken cancellationToken) =>
        {
            var validation = ValidateKey(key);
            if (validation.Error is not null)
            {
                return validation.Error;
            }

            var value = await settings.GetAsync<string?>(validation.CanonicalKey!, null, cancellationToken);
            return Results.Ok(new ConfigValueResponse(validation.CanonicalKey!, value));
        });

        group.MapPut("/{key}", async (
            string key,
            ConfigValueRequest request,
            ISystemSettingsService settings,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateKey(key);
            if (validation.Error is not null)
            {
                return validation.Error;
            }

            await settings.SetAsync(validation.CanonicalKey!, request.Value, "http-api", cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static (string? CanonicalKey, IResult? Error) ValidateKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.StartsWith("security.", StringComparison.Ordinal))
        {
            return (null, ApiErrors.ToResult(ErrorCodes.ConfigKeySecurityNamespace, StatusCodes.Status403Forbidden));
        }

        if (normalized.StartsWith("oauth.", StringComparison.Ordinal))
        {
            return (null, ApiErrors.ToResult(ErrorCodes.OAuthCredentialNamespace, StatusCodes.Status403Forbidden));
        }

        return RegisteredKeys.TryGetValue(normalized, out var canonicalKey)
            ? (canonicalKey, null)
            : (null, ApiErrors.ToResult(ErrorCodes.UnknownConfigKey, StatusCodes.Status400BadRequest));
    }

    private sealed record ConfigValueRequest(string Value);
    private sealed record ConfigValueResponse(string Key, string? Value);
}

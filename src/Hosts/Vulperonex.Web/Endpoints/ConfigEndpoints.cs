using System.Globalization;
using System.Text.Json;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Workflows.Chat;
using Vulperonex.Web.Errors;

namespace Vulperonex.Web.Endpoints;

public static class ConfigEndpoints
{
    private enum ConfigValueType
    {
        String,
        Int,
        Bool,
        Choice,
        TimeOfDay,
    }

    private sealed record ConfigKeyDescriptor(
        string CanonicalKey,
        ConfigValueType ValueType,
        IReadOnlyList<string>? AllowedValues = null);

    private static readonly IReadOnlyList<string> LogLevels =
        ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    private static readonly IReadOnlyList<string> ChatOutputDestinations =
        [WorkflowChatOutputDestination.Dual, WorkflowChatOutputDestination.OverlayOnly, WorkflowChatOutputDestination.PlatformOnly];

    private static readonly IReadOnlyDictionary<string, ConfigKeyDescriptor> RegisteredKeys = new ConfigKeyDescriptor[]
    {
        new(SystemSettingKey.StreamingPlatform, ConfigValueType.String),
        new(SystemSettingKey.BusChannelCapacity, ConfigValueType.Int),
        new(SystemSettingKey.OverlayDisplayCacheL1Capacity, ConfigValueType.Int),
        new(SystemSettingKey.OverlayDisplayCacheTtlHours, ConfigValueType.Int),
        new(SystemSettingKey.LogMinLevel, ConfigValueType.Choice, LogLevels),
        new(SystemSettingKey.LogDbRetentionDays, ConfigValueType.Int),
        new(SystemSettingKey.LogDbMaxSizeMb, ConfigValueType.Int),
        new(SystemSettingKey.LogFileRetentionDays, ConfigValueType.Int),
        new(SystemSettingKey.OverlayChatPreset, ConfigValueType.String),
        new(SystemSettingKey.OverlayMemberPreset, ConfigValueType.String),
        new(SystemSettingKey.OverlayAlertsPreset, ConfigValueType.String),
        new(SystemSettingKey.OverlayChatShowMemberCard, ConfigValueType.Bool),
        new(SystemSettingKey.OverlayChatAssistantDisplayName, ConfigValueType.String),
        new(SystemSettingKey.OverlayChatAssistantAvatarUrl, ConfigValueType.String),
        new(SystemSettingKey.OverlayChatCheckInDisplayName, ConfigValueType.String),
        new(SystemSettingKey.CheckInResetTimeLocal, ConfigValueType.TimeOfDay),
        new(SystemSettingKey.CheckInRepeatCardEnabled, ConfigValueType.Bool),
        new(SystemSettingKey.TwitchClientId, ConfigValueType.String),
        new(SystemSettingKey.TwitchChannelName, ConfigValueType.String),
        new(SystemSettingKey.OverlayMemberBackgroundUrl, ConfigValueType.String),
        new(SystemSettingKey.OverlayMemberStampUrl, ConfigValueType.String),
        new(SystemSettingKey.WorkflowChatOutputDestination, ConfigValueType.Choice, ChatOutputDestinations),
        new(SystemSettingKey.SimulationAllowPersistentWrites, ConfigValueType.Bool),
    }.ToDictionary(descriptor => descriptor.CanonicalKey.ToLowerInvariant(), descriptor => descriptor, StringComparer.Ordinal);

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

            var descriptor = validation.Descriptor!;
            // Values may be stored as JSON string, number, or boolean depending on the
            // key's type; read the raw element and render a uniform string response.
            var element = await settings.GetAsync<JsonElement?>(descriptor.CanonicalKey, null, cancellationToken);
            var value = element switch
            {
                null => null,
                { ValueKind: JsonValueKind.String } stringElement => stringElement.GetString(),
                { ValueKind: JsonValueKind.Null } or { ValueKind: JsonValueKind.Undefined } => null,
                { } otherElement => otherElement.GetRawText(),
            };
            return Results.Ok(new ConfigValueResponse(descriptor.CanonicalKey, value));
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

            var descriptor = validation.Descriptor!;
            var stored = await TrySetTypedAsync(descriptor, request.Value, settings, cancellationToken);
            return stored
                ? Results.NoContent()
                : ApiErrors.ToResult(ErrorCodes.InvalidConfigValue, StatusCodes.Status400BadRequest);
        });

        return endpoints;
    }

    private static async Task<bool> TrySetTypedAsync(
        ConfigKeyDescriptor descriptor,
        string? rawValue,
        ISystemSettingsService settings,
        CancellationToken cancellationToken)
    {
        const string category = "http-api";
        var value = rawValue?.Trim();
        if (value is null)
        {
            return false;
        }

        switch (descriptor.ValueType)
        {
            case ConfigValueType.Int:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue) || intValue < 0)
                {
                    return false;
                }

                await settings.SetAsync(descriptor.CanonicalKey, intValue, category, cancellationToken);
                return true;

            case ConfigValueType.Bool:
                if (!bool.TryParse(value, out var boolValue))
                {
                    return false;
                }

                await settings.SetAsync(descriptor.CanonicalKey, boolValue, category, cancellationToken);
                return true;

            case ConfigValueType.Choice:
                var match = descriptor.AllowedValues!
                    .FirstOrDefault(allowed => string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    return false;
                }

                await settings.SetAsync(descriptor.CanonicalKey, match, category, cancellationToken);
                return true;

            case ConfigValueType.TimeOfDay:
                if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                {
                    return false;
                }

                await settings.SetAsync(descriptor.CanonicalKey, time.ToString("HH:mm", CultureInfo.InvariantCulture), category, cancellationToken);
                return true;

            default:
                await settings.SetAsync(descriptor.CanonicalKey, value, category, cancellationToken);
                return true;
        }
    }

    private static (ConfigKeyDescriptor? Descriptor, IResult? Error) ValidateKey(string key)
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

        return RegisteredKeys.TryGetValue(normalized, out var descriptor)
            ? (descriptor, null)
            : (null, ApiErrors.ToResult(ErrorCodes.UnknownConfigKey, StatusCodes.Status400BadRequest));
    }

    private sealed record ConfigValueRequest(string Value);
    private sealed record ConfigValueResponse(string Key, string? Value);
}

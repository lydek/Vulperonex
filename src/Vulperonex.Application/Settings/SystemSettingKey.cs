namespace Vulperonex.Application.Settings;

public static class SystemSettingKey
{
    private const string ModuleEnabledPrefix = "modules.enabled.";
    public const string OAuthTwitchRefreshToken = "oauth.twitch.refresh_token";
    public const string StreamingPlatform = "streaming.platform";
    public const string BusChannelCapacity = "bus.channel_capacity";
    public const string OverlayDisplayCacheL1Capacity = "overlay.display_cache_l1_capacity";
    public const string OverlayDisplayCacheTtlHours = "overlay.display_cache_ttl_hours";
    public const string LogMinLevel = "log.min_level";
    public const string LogDbRetentionDays = "log.db_retention_days";
    public const string LogDbMaxSizeMb = "log.db_max_size_mb";
    public const string LogFileRetentionDays = "log.file_retention_days";
    public const string WorkflowTemplateStrictMissing = "workflow.template.strict_missing";
    public const string ChatOutboxPerSecond = "chat.outbox.per_second";
    public const string ChatOutboxDedupTtlHours = "chat.outbox.dedup_ttl_hours";
    public const string OverlayChatPreset = "overlay.chat.preset";
    public const string OverlayMemberPreset = "overlay.member.preset";
    public const string OverlayAlertsPreset = "overlay.alerts.preset";
    public const string OverlayChatShowMemberCard = "overlay.chat.show_member_card";
    public const string TwitchClientId = "twitch.client_id";
    public const string TwitchChannelName = "twitch.channel_name";
    public const string OverlayMemberBackgroundUrl = "overlay.member.background_url";
    public const string OverlayMemberStampUrl = "overlay.member.stamp_url";
    public const string MembersAuditRetentionDays = "members.audit_retention_days";
    public const string OverlayLanAccessKey = "overlay.lan.access_key";

    public static string ModuleEnabled(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new ArgumentException("Module name must not be empty.", nameof(moduleName));
        }

        return $"{ModuleEnabledPrefix}{moduleName.Trim().ToLowerInvariant()}";
    }

    public static bool TryParseModuleEnabledKey(string key, out string? moduleName)
    {
        moduleName = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalized = key.Trim().ToLowerInvariant();
        if (!normalized.StartsWith(ModuleEnabledPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        moduleName = normalized[ModuleEnabledPrefix.Length..];
        return moduleName.Length > 0;
    }
}

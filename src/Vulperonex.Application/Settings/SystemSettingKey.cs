namespace Vulperonex.Application.Settings;

public static class SystemSettingKey
{
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
}

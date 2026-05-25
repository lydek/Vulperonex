using FluentAssertions;
using Vulperonex.Application.Settings;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Settings;

public sealed class SystemSettingKeyTests
{
    [Fact]
    public void Given_SystemSettingKeys_When_Inspected_Then_AllKeysAreCanonicalLowercase()
    {
        var keys = typeof(SystemSettingKey)
            .GetFields()
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        keys.Should().OnlyContain(key => key == key.ToLowerInvariant());
        keys.Should().OnlyContain(key => !string.IsNullOrWhiteSpace(key));
        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Given_PhaseTwoSettings_When_Inspected_Then_RequiredKeysExist()
    {
        var keys = typeof(SystemSettingKey)
            .GetFields()
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        keys.Should().BeEquivalentTo(
            "oauth.twitch.refresh_token",
            "streaming.platform",
            "bus.channel_capacity",
            "overlay.display_cache_l1_capacity",
            "overlay.display_cache_ttl_hours",
            "log.min_level",
            "log.db_retention_days",
            "log.db_max_size_mb",
            "log.file_retention_days",
            "workflow.template.strict_missing",
            "chat.outbox.per_second",
            "chat.outbox.dedup_ttl_hours",
            "overlay.chat.preset",
            "overlay.member.preset",
            "overlay.alerts.preset",
            "overlay.chat.show_member_card",
            "twitch.client_id",
            "overlay.member.background_url",
            "overlay.member.stamp_url",
            "members.audit_retention_days");
    }
}

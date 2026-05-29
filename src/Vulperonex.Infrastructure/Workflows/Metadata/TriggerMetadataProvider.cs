using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Infrastructure.Workflows.Metadata;

public sealed class TriggerMetadataProvider(IStreamEventTypeRegistry eventTypeRegistry) : ITriggerMetadataProvider
{
    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["user.message"] = "User Message",
        ["user.donated"] = "User Donated",
        ["user.subscribed"] = "User Subscribed",
        ["user.gifted_sub"] = "User Gifted Subscription",
        ["channel.raided"] = "Channel Raided",
        ["reward.redeemed"] = "Reward Redeemed",
        ["workflow.timer"] = "Workflow Timer"
    };

    public IReadOnlyList<EventTypeMetadataDto> GetAvailableEventTypes()
    {
        return eventTypeRegistry.GetAll()
            .Select(metadata => new EventTypeMetadataDto(
                metadata.Key,
                DisplayNames.TryGetValue(metadata.Key, out var displayName) ? displayName : FormatDisplayName(metadata.Key),
                metadata.Description))
            .ToList();
    }

    public IReadOnlyList<FilterFieldDto> GetFilterFieldsFor(string eventTypeKey)
    {
        if (string.IsNullOrWhiteSpace(eventTypeKey))
        {
            return Array.Empty<FilterFieldDto>();
        }

        if (eventTypeKey.Equals("user.message", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new FilterFieldDto("CommandName", "Command Name", "string", Help: "Trigger only on messages matching this command, e.g. !checkin"),
                new FilterFieldDto("Prefix", "Prefix", "string", Help: "Trigger on messages starting with this prefix, e.g. !help")
            };
        }

        if (eventTypeKey.Equals("user.donated", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new FilterFieldDto("MinAmount", "Minimum Bits", "number", Help: "Trigger when bits given is equal or greater than this value")
            };
        }

        if (eventTypeKey.Equals("user.subscribed", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new FilterFieldDto("Tier", "Subscription Tier", "string", Options: new[] { "1000", "2000", "3000" }, Help: "Trigger only for specific sub tier", OptionLabels: new[] { "Tier 1", "Tier 2", "Tier 3" })
            };
        }

        if (eventTypeKey.Equals("user.gifted_sub", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new FilterFieldDto("Tier", "Subscription Tier", "string", Options: new[] { "1000", "2000", "3000" }, Help: "Trigger only for specific sub tier", OptionLabels: new[] { "Tier 1", "Tier 2", "Tier 3" }),
                new FilterFieldDto("MinGiftCount", "Minimum Gift Count", "number", Help: "Trigger only if gifted count is equal or greater than this")
            };
        }

        if (eventTypeKey.Equals("channel.raided", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new FilterFieldDto("MinViewers", "Minimum Viewers", "number", Help: "Trigger only if raiding viewers are equal or greater than this")
            };
        }

        if (eventTypeKey.Equals("reward.redeemed", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new FilterFieldDto("RewardName", "Reward Name", "string", Help: "Trigger only on specific channel point redemption, e.g. Lottery Ticket")
            };
        }

        if (eventTypeKey.Equals("workflow.timer", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new FilterFieldDto("TimerId", "Timer ID", "string", Help: "Trigger only on a specific timer id from the rule timer configuration")
            };
        }

        return Array.Empty<FilterFieldDto>();
    }

    public IReadOnlyList<string> GetValidVariablesFor(string eventTypeKey)
    {
        var common = new List<string> { "EventId", "EventTypeKey", "Platform", "OccurredAt" };

        if (string.IsNullOrWhiteSpace(eventTypeKey))
        {
            return common;
        }

        if (eventTypeKey.Equals("user.message", StringComparison.OrdinalIgnoreCase))
        {
            common.Add("MessageText");
        }
        else if (eventTypeKey.Equals("user.donated", StringComparison.OrdinalIgnoreCase))
        {
            common.Add("TotalBitsGiven");
        }
        else if (eventTypeKey.Equals("user.subscribed", StringComparison.OrdinalIgnoreCase))
        {
            common.Add("Tier");
        }
        else if (eventTypeKey.Equals("user.gifted_sub", StringComparison.OrdinalIgnoreCase))
        {
            common.Add("Tier");
            common.Add("GiftCount");
        }
        else if (eventTypeKey.Equals("channel.raided", StringComparison.OrdinalIgnoreCase))
        {
            common.Add("ViewerCount");
        }
        else if (eventTypeKey.Equals("reward.redeemed", StringComparison.OrdinalIgnoreCase))
        {
            common.Add("RewardId");
            common.Add("RewardTitle");
            common.Add("RedemptionId");
        }
        else if (eventTypeKey.Equals("workflow.timer", StringComparison.OrdinalIgnoreCase))
        {
            common.AddRange(new[] { "Depth", "Payload", "Payload.TimerId", "Payload.RuleId", "Payload.IntervalSeconds" });
        }

        return common;
    }

    private static string FormatDisplayName(string key)
    {
        var parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return key;
        return string.Join(" ", parts.Select(part => char.ToUpper(part[0]) + part[1..]));
    }
}

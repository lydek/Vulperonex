namespace Vulperonex.Application.Workflows.Actions;

public static class ChatCommandParser
{
    public static IReadOnlyDictionary<string, object?> Parse(string message, string? commandPrefix = null)
    {
        var normalizedMessage = message.Trim();
        var commandName = string.Empty;
        var argsText = normalizedMessage;

        if (!string.IsNullOrWhiteSpace(commandPrefix))
        {
            var prefix = commandPrefix.Trim();
            var prefixWithoutBang = prefix.TrimStart('!');
            if (normalizedMessage.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                commandName = prefixWithoutBang;
                argsText = string.Empty;
            }
            else if (normalizedMessage.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                commandName = prefixWithoutBang;
                argsText = normalizedMessage[prefix.Length..].TrimStart();
            }
        }
        else if (normalizedMessage.StartsWith('!'))
        {
            var firstSpace = normalizedMessage.IndexOf(' ');
            commandName = firstSpace < 0
                ? normalizedMessage[1..]
                : normalizedMessage[1..firstSpace];
            argsText = firstSpace < 0 ? string.Empty : normalizedMessage[(firstSpace + 1)..].TrimStart();
        }

        var args = SplitArgs(argsText);
        var arg1 = args.Count > 0 ? args[0] : string.Empty;
        var arg2 = args.Count > 1 ? args[1] : string.Empty;
        var arg3 = args.Count > 2 ? args[2] : string.Empty;
        var target = StripMentionPrefix(arg1);

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CommandName"] = commandName,
            ["ArgsText"] = argsText,
            ["Arg1"] = arg1,
            ["Arg2"] = arg2,
            ["Arg3"] = arg3,
            ["Target"] = target,
            ["TargetLogin"] = target,
            ["Mention"] = arg1.StartsWith('@') ? arg1 : string.Empty,
            ["HasTarget"] = target.Length > 0,
        };
    }

    private static IReadOnlyList<string> SplitArgs(string argsText)
    {
        if (string.IsNullOrWhiteSpace(argsText))
        {
            return [];
        }

        return argsText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static string StripMentionPrefix(string value)
    {
        return value.Trim().TrimStart('@');
    }
}

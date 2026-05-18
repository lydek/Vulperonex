internal static class CommandCompletion
{
    private static readonly string[] BuiltInCommands = ["exit", "quit"];

    public static string Complete(string line, ICommandDispatcher dispatcher)
    {
        var tokens = SplitPreservingCurrentToken(line);
        tokens = NormalizeLeadingTokens(tokens, dispatcher);
        var suggestions = GetSuggestions(tokens, dispatcher);
        if (suggestions.Count != 1)
        {
            return line;
        }

        return ApplyCompletion(line, tokens, suggestions[0], dispatcher);
    }

    public static CompletionCycle StartCycle(string line, ICommandDispatcher dispatcher)
    {
        var tokens = SplitPreservingCurrentToken(line);
        tokens = NormalizeLeadingTokens(tokens, dispatcher);
        return new CompletionCycle(line, tokens, GetSuggestions(tokens, dispatcher), -1);
    }

    public static string ApplyCycle(CompletionCycle cycle, ICommandDispatcher dispatcher, out CompletionCycle nextCycle)
    {
        if (cycle.Suggestions.Count == 0)
        {
            nextCycle = cycle;
            return cycle.BaseLine;
        }

        var nextIndex = cycle.Index == cycle.Suggestions.Count - 1 ? 0 : cycle.Index + 1;
        nextCycle = cycle with { Index = nextIndex };
        return ApplyCompletion(cycle.BaseLine, cycle.Tokens, cycle.Suggestions[nextIndex], dispatcher);
    }

    private static IReadOnlyList<string> GetSuggestions(string[] tokens, ICommandDispatcher dispatcher)
    {
        var suggestions = dispatcher.GetSuggestions(tokens);
        if (tokens.Length <= 1)
        {
            var prefix = tokens.Length == 0 ? string.Empty : tokens[0];
            suggestions = suggestions
                .Concat(BuiltInCommands.Where(command => command.StartsWith(prefix, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        return suggestions;
    }

    private static string ApplyCompletion(
        string line,
        string[] tokens,
        string completed,
        ICommandDispatcher dispatcher)
    {
        var nextLine = JoinCompletedTokens(tokens, completed);
        return HasChildSuggestions(tokens, completed, dispatcher)
            ? nextLine + " "
            : nextLine;
    }

    private static bool HasChildSuggestions(string[] tokens, string completed, ICommandDispatcher dispatcher)
    {
        var completedTokens = tokens.Length == 0
            ? [completed, string.Empty]
            : tokens[..^1].Concat([completed, string.Empty]).ToArray();
        var suggestions = dispatcher.GetSuggestions(completedTokens);
        return suggestions.Count > 0 && suggestions.Any(suggestion => !suggestion.StartsWith("--", StringComparison.Ordinal));
    }

    private static string[] NormalizeLeadingTokens(string[] tokens, ICommandDispatcher dispatcher)
    {
        if (tokens.Length <= 1)
        {
            return tokens;
        }

        var normalized = tokens.ToArray();
        for (var index = 0; index < normalized.Length - 1; index++)
        {
            var path = normalized[..(index + 1)];
            var suggestions = GetSuggestions(path, dispatcher);
            if (suggestions.Count != 1)
            {
                continue;
            }

            normalized[index] = suggestions[0];
        }

        return normalized;
    }

    private static string JoinCompletedTokens(string[] tokens, string completed)
    {
        if (tokens.Length == 0)
        {
            return completed;
        }

        var nextTokens = tokens.ToArray();
        nextTokens[^1] = completed;
        return string.Join(' ', nextTokens);
    }

    private static string[] SplitPreservingCurrentToken(string line)
    {
        if (line.Length == 0)
        {
            return [];
        }

        var tokens = line.Split([' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (line.EndsWith(" ", StringComparison.Ordinal))
        {
            tokens.Add(string.Empty);
        }

        return tokens.ToArray();
    }
}

internal sealed record CompletionCycle(
    string BaseLine,
    string[] Tokens,
    IReadOnlyList<string> Suggestions,
    int Index);

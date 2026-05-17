internal static class CommandCompletion
{
    private static readonly string[] BuiltInCommands = ["exit", "quit"];

    public static string Complete(string line, ICommandDispatcher dispatcher)
    {
        var tokens = SplitPreservingCurrentToken(line);
        var suggestions = GetSuggestions(tokens, dispatcher);
        if (suggestions.Count != 1)
        {
            return line;
        }

        var completed = suggestions[0];
        var currentToken = tokens.Length == 0 ? string.Empty : tokens[^1];
        if (string.Equals(currentToken, completed, StringComparison.Ordinal))
        {
            return line;
        }

        var prefixLength = line.Length - currentToken.Length;
        var nextLine = line[..prefixLength] + completed;
        return HasChildSuggestions(tokens, completed, dispatcher)
            ? nextLine + " "
            : nextLine;
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

    private static bool HasChildSuggestions(string[] tokens, string completed, ICommandDispatcher dispatcher)
    {
        var completedTokens = tokens.Length == 0
            ? [completed, string.Empty]
            : tokens[..^1].Concat([completed, string.Empty]).ToArray();
        return dispatcher.GetSuggestions(completedTokens).Count > 0;
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

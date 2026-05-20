using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

internal sealed record ResolvedRule(string Id, string Name, bool IsEnabled);

internal sealed record ResolvedRuleArgs(ResolvedRule Rule, bool Yes);

internal static class RuleIdentifierResolver
{
    private const int MaxCandidatesShown = 10;

    public static async Task<ResolvedRuleArgs?> ResolveAsync(
        string[] args,
        CliExecutionContext context,
        string usageKey,
        string hintKey,
        CancellationToken cancellationToken)
    {
        if (!TryParseArgs(args, out var positional, out var nameFlag, out var yes, out var parseError))
        {
            await context.FailAsync(parseError);
            return null;
        }

        if (positional is null && nameFlag is null)
        {
            await context.MissingArgsAsync(usageKey, hintKey);
            return null;
        }

        if (positional is not null && nameFlag is not null)
        {
            await context.FailAsync("INVALID_ARGS");
            return null;
        }

        var resolved = positional is not null
            ? await ResolveByPositionalAsync(positional, context, cancellationToken)
            : await ResolveByNameAsync(nameFlag!, context, cancellationToken);

        if (resolved is null)
        {
            return null;
        }

        return new ResolvedRuleArgs(resolved, yes);
    }

    private static bool TryParseArgs(
        string[] args,
        out string? positional,
        out string? nameFlag,
        out bool yes,
        out string parseError)
    {
        positional = null;
        nameFlag = null;
        yes = false;
        parseError = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg is "--yes" or "-y")
            {
                yes = true;
                continue;
            }

            if (arg == "--name")
            {
                if (index + 1 >= args.Length || nameFlag is not null)
                {
                    parseError = "INVALID_ARGS";
                    return false;
                }

                nameFlag = args[++index];
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                parseError = "INVALID_ARGS";
                return false;
            }

            if (positional is not null)
            {
                parseError = "INVALID_ARGS";
                return false;
            }

            positional = arg;
        }

        return true;
    }

    private static async Task<ResolvedRule?> ResolveByPositionalAsync(
        string positional,
        CliExecutionContext context,
        CancellationToken cancellationToken)
    {
        var direct = await context.Client.GetAsync($"/api/rules/{Uri.EscapeDataString(positional)}", cancellationToken);
        try
        {
            if (direct.IsSuccessStatusCode)
            {
                var dto = await ReadRuleSummaryAsync(direct, cancellationToken);
                if (dto is null)
                {
                    await context.FailAsync("CLI_UNEXPECTED_ERROR");
                    return null;
                }

                return dto;
            }

            if (direct.StatusCode != HttpStatusCode.NotFound)
            {
                // Surface server error via the standard response writer.
                _ = await context.WriteResponseAsync(direct);
                return null;
            }
        }
        finally
        {
            direct.Dispose();
        }

        var candidates = await ListRulesAsync(context, cancellationToken);
        if (candidates is null)
        {
            return null;
        }

        var matches = candidates
            .Where(rule => rule.Id.StartsWith(positional, StringComparison.Ordinal))
            .ToList();
        return await PickSingleAsync(matches, context);
    }

    private static async Task<ResolvedRule?> ResolveByNameAsync(
        string nameFlag,
        CliExecutionContext context,
        CancellationToken cancellationToken)
    {
        var candidates = await ListRulesAsync(context, cancellationToken);
        if (candidates is null)
        {
            return null;
        }

        var matches = candidates
            .Where(rule => string.Equals(rule.Name, nameFlag, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return await PickSingleAsync(matches, context);
    }

    private static async Task<ResolvedRule?> PickSingleAsync(
        IReadOnlyList<ResolvedRule> matches,
        CliExecutionContext context)
    {
        if (matches.Count == 1)
        {
            return matches[0];
        }

        if (matches.Count == 0)
        {
            await context.FailAsync("NOT_FOUND");
            return null;
        }

        await context.Error.WriteLineAsync("AMBIGUOUS_ID");
        await context.Error.WriteLineAsync(CliText.Get("resolver.candidates.header"));
        var shown = 0;
        foreach (var match in matches)
        {
            if (shown >= MaxCandidatesShown)
            {
                break;
            }

            var idShort = match.Id.Length > 8 ? match.Id[..8] : match.Id;
            var status = match.IsEnabled ? "enabled" : "disabled";
            await context.Error.WriteLineAsync($"  {idShort}  {match.Name}  {status}");
            shown++;
        }

        var remaining = matches.Count - shown;
        if (remaining > 0)
        {
            await context.Error.WriteLineAsync($"  {CliText.Format("resolver.candidates.truncated", remaining)}");
        }

        return null;
    }

    private static async Task<IReadOnlyList<ResolvedRule>?> ListRulesAsync(
        CliExecutionContext context,
        CancellationToken cancellationToken)
    {
        var response = await context.Client.GetAsync("/api/rules", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _ = await context.WriteResponseAsync(response);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var rules = await JsonSerializer.DeserializeAsync<List<RuleListItem>>(stream, context.JsonOptions, cancellationToken);
        return rules is null
            ? Array.Empty<ResolvedRule>()
            : rules.Select(rule => new ResolvedRule(rule.Id ?? string.Empty, rule.Name ?? string.Empty, rule.IsEnabled)).ToArray();
    }

    private static async Task<ResolvedRule?> ReadRuleSummaryAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var rule = await response.Content.ReadFromJsonAsync<RuleListItem>(cancellationToken: cancellationToken);
        if (rule is null || string.IsNullOrEmpty(rule.Id))
        {
            return null;
        }

        return new ResolvedRule(rule.Id, rule.Name ?? string.Empty, rule.IsEnabled);
    }

    private sealed record RuleListItem(string? Id, string? Name, bool IsEnabled);
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

internal sealed record ResolvedMember(string MemberId, string? FirstPlatform, string? FirstPlatformUserId);

internal sealed record ResolvedMemberArgs(ResolvedMember Member, bool Yes);

internal static class MemberIdentifierResolver
{
    private const int MaxCandidatesShown = 10;

    public static async Task<ResolvedMemberArgs?> ResolveAsync(
        string[] args,
        CliExecutionContext context,
        string usageKey,
        string hintKey,
        CancellationToken cancellationToken)
    {
        if (!TryParseArgs(args, out var positional, out var yes, out var parseError))
        {
            await context.FailAsync(parseError);
            return null;
        }

        if (positional is null)
        {
            await context.MissingArgsAsync(usageKey, hintKey);
            return null;
        }

        var direct = await context.Client.GetAsync($"/api/members/{Uri.EscapeDataString(positional)}", cancellationToken);
        try
        {
            if (direct.IsSuccessStatusCode)
            {
                var dto = await ReadMemberSummaryAsync(direct, cancellationToken);
                if (dto is null)
                {
                    await context.FailAsync("CLI_UNEXPECTED_ERROR");
                    return null;
                }

                return new ResolvedMemberArgs(dto, yes);
            }

            if (direct.StatusCode != HttpStatusCode.NotFound)
            {
                _ = await context.WriteResponseAsync(direct);
                return null;
            }
        }
        finally
        {
            direct.Dispose();
        }

        var candidates = await ListMembersAsync(context, cancellationToken);
        if (candidates is null)
        {
            return null;
        }

        var matches = candidates
            .Where(member => member.MemberId.StartsWith(positional, StringComparison.Ordinal))
            .ToList();
        var resolved = await PickSingleAsync(matches, context);
        return resolved is null ? null : new ResolvedMemberArgs(resolved, yes);
    }

    private static bool TryParseArgs(string[] args, out string? positional, out bool yes, out string parseError)
    {
        positional = null;
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

    private static async Task<ResolvedMember?> PickSingleAsync(
        IReadOnlyList<ResolvedMember> matches,
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

            var idShort = match.MemberId.Length > 8 ? match.MemberId[..8] : match.MemberId;
            var platformLabel = match.FirstPlatform is null ? "-" : $"{match.FirstPlatform}:{match.FirstPlatformUserId}";
            await context.Error.WriteLineAsync($"  {idShort}  {platformLabel}");
            shown++;
        }

        var remaining = matches.Count - shown;
        if (remaining > 0)
        {
            await context.Error.WriteLineAsync($"  {CliText.Format("resolver.candidates.truncated", remaining)}");
        }

        return null;
    }

    private static async Task<IReadOnlyList<ResolvedMember>?> ListMembersAsync(
        CliExecutionContext context,
        CancellationToken cancellationToken)
    {
        var response = await context.Client.GetAsync("/api/members", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _ = await context.WriteResponseAsync(response);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var members = await JsonSerializer.DeserializeAsync<List<MemberListItem>>(stream, context.JsonOptions, cancellationToken);
        return members is null
            ? Array.Empty<ResolvedMember>()
            : members.Select(ToResolved).ToArray();
    }

    private static async Task<ResolvedMember?> ReadMemberSummaryAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var member = await response.Content.ReadFromJsonAsync<MemberListItem>(cancellationToken: cancellationToken);
        if (member is null || string.IsNullOrEmpty(member.MemberId))
        {
            return null;
        }

        return ToResolved(member);
    }

    private static ResolvedMember ToResolved(MemberListItem item)
    {
        var first = item.Identities is { Count: > 0 } ? item.Identities[0] : null;
        return new ResolvedMember(item.MemberId ?? string.Empty, first?.Platform, first?.PlatformUserId);
    }

    private sealed record MemberListItem(string? MemberId, IReadOnlyList<MemberIdentityItem>? Identities);

    private sealed record MemberIdentityItem(string? Platform, string? PlatformUserId);
}

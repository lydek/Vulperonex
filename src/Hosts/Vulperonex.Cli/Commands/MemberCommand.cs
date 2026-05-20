using System.Net.Http.Json;

internal sealed class MemberCommand : CompositeConsoleCommand
{
    public MemberCommand()
    {
        AddSubCommand(new DelegateConsoleCommand("list", "command.member.list.description", "command.member.list.usage", Category, async (_, context, ct) =>
            await context.WriteResponseAsync(await context.Client.GetAsync("/api/members", ct)), ["ls"]));
        AddSubCommand(new DelegateConsoleCommand("show", "command.member.show.description", "command.member.show.usage", Category, ShowAsync, ["status", "info", "get", "find"]));
        AddSubCommand(new DelegateConsoleCommand("seed", "command.member.seed.description", "command.member.seed.usage", Category, SeedAsync, ["add", "mock"]));
        AddSubCommand(new DelegateConsoleCommand("delete", "command.member.delete.description", "command.member.delete.usage", Category, DeleteAsync, ["remove", "rm"]));
    }

    public override string Name => "member";

    public override IReadOnlyList<string> Aliases => ["m", "user", "members"];

    public override string Category => "category.workflow";

    public override string Description => CliText.Get("command.member.description");

    public override string Usage => CliText.Get("command.member.usage");

    private static async Task<int> ShowAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        var resolved = await MemberIdentifierResolver.ResolveAsync(
            args,
            context,
            "command.member.show.usage",
            "command.member.show.hint",
            ct);
        if (resolved is null)
        {
            return 1;
        }

        return await context.WriteResponseAsync(
            await context.Client.GetAsync($"/api/members/{Uri.EscapeDataString(resolved.Member.MemberId)}", ct));
    }

    private static async Task<int> SeedAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        if (args.Length < 1)
        {
            return await context.MissingArgsAsync("command.member.seed.usage", "command.member.show.hint");
        }

        var displayName = args.Length >= 2 ? string.Join(' ', args.Skip(1)) : args[0];
        var response = await context.Client.PostAsJsonAsync(
            "/api/simulate/chat",
            new
            {
                platformUserId = args[0],
                displayName,
                message = "member seed",
            },
            ct);
        var exitCode = await context.WriteResponseAsync(response, $"OK member seed requested: {args[0]}");
        if (exitCode != 0)
        {
            return exitCode;
        }

        return await TryWriteSeededMemberAsync(args[0], context, ct);
    }

    private static async Task<int> DeleteAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        var resolved = await MemberIdentifierResolver.ResolveAsync(
            args,
            context,
            "command.member.delete.usage",
            "command.member.delete.hint",
            ct);
        if (resolved is null)
        {
            return 1;
        }

        if (!await context.ConfirmAsync(
            "command.member.delete.confirm",
            BuildMemberSummary(resolved.Member),
            resolved.Yes))
        {
            return 1;
        }

        return await context.WriteResponseAsync(
            await context.Client.DeleteAsync($"/api/members/{Uri.EscapeDataString(resolved.Member.MemberId)}", ct),
            $"OK member deleted: {resolved.Member.MemberId}");
    }

    private static IReadOnlyList<string> BuildMemberSummary(ResolvedMember member)
    {
        var identity = member.FirstPlatform is null
            ? "-"
            : $"{member.FirstPlatform}:{member.FirstPlatformUserId}";
        return
        [
            $"id:       {member.MemberId}",
            $"identity: {identity}",
        ];
    }

    private static async Task<int> TryWriteSeededMemberAsync(
        string platformUserId,
        CliExecutionContext context,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }

            var response = await context.Client.GetAsync("/api/members", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return await context.WriteResponseAsync(response);
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (TryFindMemberId(payload, platformUserId, out var memberId))
            {
                await context.Output.WriteLineAsync($"OK member available: {memberId}");
                return 0;
            }
        }

        await context.Output.WriteLineAsync("OK member seed queued");
        return 0;
    }

    private static bool TryFindMemberId(string payload, string platformUserId, out string? memberId)
    {
        memberId = null;
        using var document = System.Text.Json.JsonDocument.Parse(payload);
        foreach (var member in document.RootElement.EnumerateArray())
        {
            if (!member.TryGetProperty("identities", out var identities))
            {
                continue;
            }

            foreach (var identity in identities.EnumerateArray())
            {
                if (identity.TryGetProperty("platformUserId", out var id)
                    && string.Equals(id.GetString(), platformUserId, StringComparison.Ordinal))
                {
                    memberId = member.GetProperty("memberId").GetString();
                    return true;
                }
            }
        }

        return false;
    }
}

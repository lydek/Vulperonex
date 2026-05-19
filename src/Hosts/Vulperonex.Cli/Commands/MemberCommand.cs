using System.Net.Http.Json;

internal sealed class MemberCommand : CompositeConsoleCommand
{
    public MemberCommand()
    {
        AddSubCommand(new DelegateConsoleCommand("list", "command.member.list.description", "command.member.list.usage", Category, async (_, context, ct) =>
            await context.WriteResponseAsync(await context.Client.GetAsync("/api/members", ct)), ["ls"]));
        AddSubCommand(new DelegateConsoleCommand("show", "command.member.show.description", "command.member.show.usage", Category, async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.GetAsync($"/api/members/{Uri.EscapeDataString(args[0])}", ct))
                : await context.FailAsync("UNKNOWN_COMMAND"), ["status", "info", "get", "find"]));
        AddSubCommand(new DelegateConsoleCommand("seed", "command.member.seed.description", "command.member.seed.usage", Category, async (args, context, ct) =>
        {
            if (args.Length < 1)
            {
                return await context.FailAsync("UNKNOWN_COMMAND");
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
        }, ["add", "mock"]));
        AddSubCommand(new DelegateConsoleCommand("delete", "command.member.delete.description", "command.member.delete.usage", Category, async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(
                    await context.Client.DeleteAsync($"/api/members/{Uri.EscapeDataString(args[0])}", ct),
                    $"OK member deleted: {args[0]}")
                : await context.FailAsync("UNKNOWN_COMMAND"), ["remove", "rm"]));
    }

    public override string Name => "member";

    public override IReadOnlyList<string> Aliases => ["m", "user", "members"];

    public override string Category => "category.workflow";

    public override string Description => CliText.Get("command.member.description");

    public override string Usage => CliText.Get("command.member.usage");

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

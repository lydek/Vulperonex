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
            return await context.WriteResponseAsync(response);
        }, ["add", "mock"]));
        AddSubCommand(new DelegateConsoleCommand("delete", "command.member.delete.description", "command.member.delete.usage", Category, async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.DeleteAsync($"/api/members/{Uri.EscapeDataString(args[0])}", ct))
                : await context.FailAsync("UNKNOWN_COMMAND"), ["remove", "rm"]));
    }

    public override string Name => "member";

    public override IReadOnlyList<string> Aliases => ["m", "user", "members"];

    public override string Category => "category.workflow";

    public override string Description => CliText.Get("command.member.description");

    public override string Usage => CliText.Get("command.member.usage");
}

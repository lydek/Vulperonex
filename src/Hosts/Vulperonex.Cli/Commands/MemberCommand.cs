internal sealed class MemberCommand : CompositeConsoleCommand
{
    public MemberCommand()
    {
        AddSubCommand(new DelegateConsoleCommand("list", "List members.", async (_, context, ct) =>
            await context.WriteResponseAsync(await context.Client.GetAsync("/api/members", ct))));
        AddSubCommand(new DelegateConsoleCommand("show", "Show a member.", async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.GetAsync($"/api/members/{Uri.EscapeDataString(args[0])}", ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
    }

    public override string Name => "member";

    public override string Description => "Query members.";
}

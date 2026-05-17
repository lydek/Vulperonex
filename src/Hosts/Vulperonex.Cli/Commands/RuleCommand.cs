internal sealed class RuleCommand : CompositeConsoleCommand
{
    public RuleCommand()
    {
        AddSubCommand(new DelegateConsoleCommand("list", "List workflow rules.", async (_, context, ct) =>
            await context.WriteResponseAsync(await context.Client.GetAsync("/api/rules", ct))));
        AddSubCommand(new DelegateConsoleCommand("show", "Show a workflow rule.", async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.GetAsync($"/api/rules/{Uri.EscapeDataString(args[0])}", ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
        AddSubCommand(new DelegateConsoleCommand("enable", "Enable a workflow rule.", async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.PostAsync($"/api/rules/{Uri.EscapeDataString(args[0])}/enable", null, ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
        AddSubCommand(new DelegateConsoleCommand("disable", "Disable a workflow rule.", async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.PostAsync($"/api/rules/{Uri.EscapeDataString(args[0])}/disable", null, ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
        AddSubCommand(new DelegateConsoleCommand("delete", "Delete a workflow rule.", async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.DeleteAsync($"/api/rules/{Uri.EscapeDataString(args[0])}", ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
    }

    public override string Name => "rule";

    public override string Description => "Manage workflow rules.";
}

internal sealed class RuleCommand : CompositeConsoleCommand
{
    public RuleCommand()
    {
        AddSubCommand(new DelegateConsoleCommand("list", "command.rule.list.description", "command.rule.list.usage", Category, async (_, context, ct) =>
            await context.WriteResponseAsync(await context.Client.GetAsync("/api/rules", ct))));
        AddSubCommand(new DelegateConsoleCommand("show", "command.rule.show.description", "command.rule.show.usage", Category, async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.GetAsync($"/api/rules/{Uri.EscapeDataString(args[0])}", ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
        AddSubCommand(new DelegateConsoleCommand("enable", "command.rule.enable.description", "command.rule.enable.usage", Category, async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.PutAsync($"/api/rules/{Uri.EscapeDataString(args[0])}/enable", null, ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
        AddSubCommand(new DelegateConsoleCommand("disable", "command.rule.disable.description", "command.rule.disable.usage", Category, async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.PutAsync($"/api/rules/{Uri.EscapeDataString(args[0])}/disable", null, ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
        AddSubCommand(new DelegateConsoleCommand("delete", "command.rule.delete.description", "command.rule.delete.usage", Category, async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.DeleteAsync($"/api/rules/{Uri.EscapeDataString(args[0])}", ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
    }

    public override string Name => "rule";

    public override IReadOnlyList<string> Aliases => ["r"];

    public override string Category => "category.workflow";

    public override string Description => CliText.Get("command.rule.description");

    public override string Usage => CliText.Get("command.rule.usage");
}

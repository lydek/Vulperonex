using System.Text;

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
        AddSubCommand(new DelegateConsoleCommand("create", "command.rule.create.description", "command.rule.create.usage", Category, async (args, context, ct) =>
        {
            if (args.Length < 1)
            {
                return await context.FailAsync("UNKNOWN_COMMAND");
            }

            var content = await ReadJsonFileAsync(args[0], context);
            if (content is null)
            {
                return 1;
            }

            using var body = new StringContent(content, Encoding.UTF8, "application/json");
            return await context.WriteResponseAsync(await context.Client.PostAsync("/api/rules", body, ct));
        }, ["add", "new"]));
        AddSubCommand(new DelegateConsoleCommand("update", "command.rule.update.description", "command.rule.update.usage", Category, async (args, context, ct) =>
        {
            if (args.Length < 2)
            {
                return await context.FailAsync("UNKNOWN_COMMAND");
            }

            var content = await ReadJsonFileAsync(args[1], context);
            if (content is null)
            {
                return 1;
            }

            using var body = new StringContent(content, Encoding.UTF8, "application/json");
            return await context.WriteResponseAsync(await context.Client.PutAsync($"/api/rules/{Uri.EscapeDataString(args[0])}", body, ct));
        }, ["set"]));
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

    private static async Task<string?> ReadJsonFileAsync(string path, CliExecutionContext context)
    {
        if (!File.Exists(path))
        {
            await context.Error.WriteLineAsync("FILE_NOT_FOUND");
            return null;
        }

        return await File.ReadAllTextAsync(path);
    }
}

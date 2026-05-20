using System.Text;

internal sealed class RuleCommand : CompositeConsoleCommand
{
    public RuleCommand()
    {
        AddSubCommand(new DelegateConsoleCommand("list", "command.rule.list.description", "command.rule.list.usage", Category, async (_, context, ct) =>
            await context.WriteResponseAsync(await context.Client.GetAsync("/api/rules", ct))));
        AddSubCommand(new DelegateConsoleCommand("show", "command.rule.show.description", "command.rule.show.usage", Category, ShowAsync));
        AddSubCommand(new DelegateConsoleCommand("create", "command.rule.create.description", "command.rule.create.usage", Category, CreateAsync, ["add", "new"]));
        AddSubCommand(new DelegateConsoleCommand("update", "command.rule.update.description", "command.rule.update.usage", Category, UpdateAsync, ["set"]));
        AddSubCommand(new DelegateConsoleCommand("enable", "command.rule.enable.description", "command.rule.enable.usage", Category, EnableAsync));
        AddSubCommand(new DelegateConsoleCommand("disable", "command.rule.disable.description", "command.rule.disable.usage", Category, DisableAsync));
        AddSubCommand(new DelegateConsoleCommand("delete", "command.rule.delete.description", "command.rule.delete.usage", Category, DeleteAsync));
    }

    public override string Name => "rule";

    public override IReadOnlyList<string> Aliases => ["r"];

    public override string Category => "category.workflow";

    public override string Description => CliText.Get("command.rule.description");

    public override string Usage => CliText.Get("command.rule.usage");

    private static async Task<int> ShowAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        var resolved = await RuleIdentifierResolver.ResolveAsync(
            args,
            context,
            "command.rule.show.usage",
            "command.rule.show.hint",
            ct);
        if (resolved is null)
        {
            return 1;
        }

        return await context.WriteResponseAsync(
            await context.Client.GetAsync($"/api/rules/{Uri.EscapeDataString(resolved.Rule.Id)}", ct));
    }

    private static async Task<int> CreateAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        if (args.Length < 1)
        {
            return await context.MissingArgsAsync("command.rule.create.usage", "command.rule.create.hint");
        }

        var content = await ReadJsonFileAsync(args[0], context);
        if (content is null)
        {
            return 1;
        }

        using var body = new StringContent(content, Encoding.UTF8, "application/json");
        return await context.WriteResponseAsync(await context.Client.PostAsync("/api/rules", body, ct));
    }

    private static async Task<int> UpdateAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        if (args.Length < 2)
        {
            return await context.MissingArgsAsync("command.rule.update.usage", "command.rule.update.hint");
        }

        // Resolve identifier from all args except the final positional (file path).
        var filePath = args[^1];
        var identifierArgs = args[..^1];
        var resolved = await RuleIdentifierResolver.ResolveAsync(
            identifierArgs,
            context,
            "command.rule.update.usage",
            "command.rule.update.hint",
            ct);
        if (resolved is null)
        {
            return 1;
        }

        var content = await ReadJsonFileAsync(filePath, context);
        if (content is null)
        {
            return 1;
        }

        using var body = new StringContent(content, Encoding.UTF8, "application/json");
        return await context.WriteResponseAsync(
            await context.Client.PutAsync($"/api/rules/{Uri.EscapeDataString(resolved.Rule.Id)}", body, ct));
    }

    private static async Task<int> EnableAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        var resolved = await RuleIdentifierResolver.ResolveAsync(
            args,
            context,
            "command.rule.enable.usage",
            "command.rule.enable.hint",
            ct);
        if (resolved is null)
        {
            return 1;
        }

        return await context.WriteResponseAsync(
            await context.Client.PutAsync($"/api/rules/{Uri.EscapeDataString(resolved.Rule.Id)}/enable", null, ct),
            $"OK rule enabled: {resolved.Rule.Id}");
    }

    private static async Task<int> DisableAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        var resolved = await RuleIdentifierResolver.ResolveAsync(
            args,
            context,
            "command.rule.disable.usage",
            "command.rule.disable.hint",
            ct);
        if (resolved is null)
        {
            return 1;
        }

        if (!await context.ConfirmAsync(
            "command.rule.disable.confirm",
            BuildRuleSummary(resolved.Rule),
            resolved.Yes))
        {
            return 1;
        }

        return await context.WriteResponseAsync(
            await context.Client.PutAsync($"/api/rules/{Uri.EscapeDataString(resolved.Rule.Id)}/disable", null, ct),
            $"OK rule disabled: {resolved.Rule.Id}");
    }

    private static async Task<int> DeleteAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        var resolved = await RuleIdentifierResolver.ResolveAsync(
            args,
            context,
            "command.rule.delete.usage",
            "command.rule.delete.hint",
            ct);
        if (resolved is null)
        {
            return 1;
        }

        if (!await context.ConfirmAsync(
            "command.rule.delete.confirm",
            BuildRuleSummary(resolved.Rule),
            resolved.Yes))
        {
            return 1;
        }

        return await context.WriteResponseAsync(
            await context.Client.DeleteAsync($"/api/rules/{Uri.EscapeDataString(resolved.Rule.Id)}", ct),
            $"OK rule deleted: {resolved.Rule.Id}");
    }

    private static IReadOnlyList<string> BuildRuleSummary(ResolvedRule rule)
    {
        return
        [
            $"id:     {rule.Id}",
            $"name:   {rule.Name}",
            $"status: {(rule.IsEnabled ? "enabled" : "disabled")}",
        ];
    }

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

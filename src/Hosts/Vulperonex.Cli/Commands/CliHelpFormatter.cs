internal static class CliHelpFormatter
{
    private static readonly string[] CategoryOrder =
    [
        "category.system",
        "category.workflow",
        "category.stream",
        "category.integration",
    ];

    public static async Task WriteGlobalHelpAsync(TextWriter output, IEnumerable<IConsoleCommand> commands)
    {
        await output.WriteLineAsync($"========================= {CliText.Get("help.title")} =========================");
        await output.WriteLineAsync();

        var orderedCommands = commands
            .OrderBy(command => Array.IndexOf(CategoryOrder, command.Category) is var index && index >= 0 ? index : int.MaxValue)
            .ThenBy(command => command.Name, StringComparer.Ordinal)
            .GroupBy(command => command.Category);

        foreach (var group in orderedCommands)
        {
            await output.WriteLineAsync($"[{CliText.Get(group.Key)}]");
            foreach (var command in group.OrderBy(command => command.Name, StringComparer.Ordinal))
            {
                await WriteSummaryLineAsync(output, command);
            }

            await output.WriteLineAsync();
        }

        await output.WriteLineAsync(CliText.Get("help.tip"));
    }

    public static async Task WriteCommandHelpAsync(TextWriter output, IConsoleCommand command)
    {
        await output.WriteLineAsync($"--- {CliText.Format("help.subcommands", command.Name)} ---");
        await output.WriteLineAsync($"{CliText.Get("help.usage")}: {command.Usage}");

        if (command is not ICommandDispatcher dispatcher)
        {
            await output.WriteLineAsync(command.Description);
            return;
        }

        foreach (var child in dispatcher.GetAllCommands().OrderBy(child => child.Name, StringComparer.Ordinal))
        {
            await WriteSummaryLineAsync(output, child);
        }
    }

    private static async Task WriteSummaryLineAsync(TextWriter output, IConsoleCommand command)
    {
        var names = command.Aliases.Count == 0
            ? command.Name
            : string.Join('/', new[] { command.Name }.Concat(command.Aliases));
        var summary = command is ICommandDispatcher dispatcher
            ? $"{command.Description} ({string.Join('|', dispatcher.GetAllCommands().Select(child => child.Name))})"
            : command.Description;

        await output.WriteLineAsync($"  {names.PadRight(24)} {summary}");
    }
}

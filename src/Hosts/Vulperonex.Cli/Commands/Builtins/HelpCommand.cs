internal sealed class HelpCommand(Func<IEnumerable<IConsoleCommand>> commands) : IConsoleCommand
{
    public string Name => "help";

    public IReadOnlyList<string> Aliases => ["?"];

    public string Description => "Show available commands.";

    public async Task<int> ExecuteAsync(
        string triggerName,
        string[] args,
        CliExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var command in commands().OrderBy(command => command.Name, StringComparer.Ordinal))
        {
            await WriteCommandAsync(context.Output, command.Name, command);
        }

        return 0;
    }

    public IReadOnlyList<string> GetSuggestions(string[] args)
    {
        return [];
    }

    private static async Task WriteCommandAsync(TextWriter output, string path, IConsoleCommand command)
    {
        await output.WriteLineAsync($"{path} - {command.Description}");
        if (command is not ICommandDispatcher dispatcher)
        {
            return;
        }

        foreach (var child in dispatcher.GetAllCommands().OrderBy(child => child.Name, StringComparer.Ordinal))
        {
            await WriteCommandAsync(output, $"{path} {child.Name}", child);
        }
    }
}

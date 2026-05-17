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
            await context.Output.WriteLineAsync($"{command.Name} - {command.Description}");
        }

        return 0;
    }

    public IReadOnlyList<string> GetSuggestions(string[] args)
    {
        return [];
    }
}

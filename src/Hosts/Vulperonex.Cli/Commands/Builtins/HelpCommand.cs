internal sealed class HelpCommand(Func<IEnumerable<IConsoleCommand>> commands) : IConsoleCommand
{
    public string Name => "help";

    public IReadOnlyList<string> Aliases => ["?", "man"];

    public string Category => "category.system";

    public string Description => CliText.Get("command.help.description");

    public string Usage => CliText.Get("command.help.usage");

    public async Task<int> ExecuteAsync(
        string triggerName,
        string[] args,
        CliExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await CliHelpFormatter.WriteGlobalHelpAsync(context.Output, commands());
        return 0;
    }

    public IReadOnlyList<string> GetSuggestions(string[] args)
    {
        return [];
    }
}

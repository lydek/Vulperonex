internal interface IConsoleCommand
{
    string Name { get; }

    IReadOnlyList<string> Aliases { get; }

    string Description { get; }

    Task<int> ExecuteAsync(
        string triggerName,
        string[] args,
        CliExecutionContext context,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetSuggestions(string[] args);
}

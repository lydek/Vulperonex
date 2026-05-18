internal interface IConsoleCommand
{
    string Name { get; }

    IReadOnlyList<string> Aliases { get; }

    string Category { get; }

    string Description { get; }

    string Usage { get; }

    Task<int> ExecuteAsync(
        string triggerName,
        string[] args,
        CliExecutionContext context,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetSuggestions(string[] args);
}

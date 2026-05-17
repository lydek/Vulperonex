internal interface ICommandDispatcher
{
    Task<int> DispatchAsync(
        string[] args,
        CliExecutionContext context,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetSuggestions(string[] args);

    IEnumerable<IConsoleCommand> GetAllCommands();
}

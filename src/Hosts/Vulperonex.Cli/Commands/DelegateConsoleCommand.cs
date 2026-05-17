internal sealed class DelegateConsoleCommand(
    string name,
    string description,
    Func<string[], CliExecutionContext, CancellationToken, Task<int>> execute,
    IReadOnlyList<string>? aliases = null) : IConsoleCommand
{
    public string Name { get; } = name;

    public IReadOnlyList<string> Aliases { get; } = aliases ?? [];

    public string Description { get; } = description;

    public Task<int> ExecuteAsync(
        string triggerName,
        string[] args,
        CliExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return execute(args, context, cancellationToken);
    }

    public IReadOnlyList<string> GetSuggestions(string[] args)
    {
        return [];
    }
}

internal sealed class DelegateConsoleCommand(
    string name,
    string descriptionKey,
    string usageKey,
    string category,
    Func<string[], CliExecutionContext, CancellationToken, Task<int>> execute,
    IReadOnlyList<string>? aliases = null) : IConsoleCommand
{
    public string Name { get; } = name;

    public IReadOnlyList<string> Aliases { get; } = aliases ?? [];

    public string Category { get; } = category;

    public string Description => CliText.Get(descriptionKey);

    public string Usage => CliText.Get(usageKey);

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

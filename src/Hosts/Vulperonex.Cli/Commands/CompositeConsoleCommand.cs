internal abstract class CompositeConsoleCommand : IConsoleCommand, ICommandDispatcher
{
    private readonly List<IConsoleCommand> _subCommands = [];

    public abstract string Name { get; }

    public virtual IReadOnlyList<string> Aliases => [];

    public abstract string Category { get; }

    public abstract string Description { get; }

    public abstract string Usage { get; }

    protected void AddSubCommand(IConsoleCommand command)
    {
        _subCommands.Add(command);
    }

    public Task<int> ExecuteAsync(
        string triggerName,
        string[] args,
        CliExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return DispatchAsync(args, context, cancellationToken);
    }

    public async Task<int> DispatchAsync(
        string[] args,
        CliExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (args.Length == 0)
        {
            await CliHelpFormatter.WriteCommandHelpAsync(context.Output, this);
            return 0;
        }

        var command = Find(args[0]);
        return command is null
            ? await context.FailAsync("UNKNOWN_COMMAND")
            : await command.ExecuteAsync(args[0], args.Skip(1).ToArray(), context, cancellationToken);
    }

    public IReadOnlyList<string> GetSuggestions(string[] args)
    {
        if (args.Length <= 1)
        {
            var prefix = args.Length == 0 ? string.Empty : args[0];
            return _subCommands
                .Select(command => command.Name)
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        return Find(args[0])?.GetSuggestions(args.Skip(1).ToArray()) ?? [];
    }

    public IEnumerable<IConsoleCommand> GetAllCommands()
    {
        return _subCommands;
    }

    private IConsoleCommand? Find(string name)
    {
        return _subCommands.FirstOrDefault(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal)
            || command.Aliases.Contains(name, StringComparer.Ordinal));
    }
}

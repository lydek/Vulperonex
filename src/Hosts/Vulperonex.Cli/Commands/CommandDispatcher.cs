internal sealed class CommandDispatcher(IEnumerable<IConsoleCommand> commands) : ICommandDispatcher
{
    private readonly List<IConsoleCommand> _commands = commands.ToList();

    public async Task<int> DispatchAsync(
        string[] args,
        CliExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (args.Length == 0)
        {
            return await context.FailAsync("UNKNOWN_COMMAND");
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
            return _commands
                .Select(command => command.Name)
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        return Find(args[0])?.GetSuggestions(args.Skip(1).ToArray()) ?? [];
    }

    public IEnumerable<IConsoleCommand> GetAllCommands()
    {
        return _commands;
    }

    private IConsoleCommand? Find(string name)
    {
        return _commands.FirstOrDefault(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal)
            || command.Aliases.Contains(name, StringComparer.Ordinal));
    }
}

internal sealed class InteractiveSession(
    ICommandDispatcher dispatcher,
    CliExecutionContext context,
    TextReader input)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await input.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var args = CommandLineTokenizer.Split(line);
            if (args.Length == 0)
            {
                continue;
            }

            if (string.Equals(args[0], "exit", StringComparison.Ordinal)
                || string.Equals(args[0], "quit", StringComparison.Ordinal))
            {
                return 0;
            }

            await dispatcher.DispatchAsync(args, context, cancellationToken);
        }
    }
}

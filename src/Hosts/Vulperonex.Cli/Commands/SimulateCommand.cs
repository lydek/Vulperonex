using System.Net.Http.Json;

internal sealed class SimulateCommand : IConsoleCommand
{
    public string Name => "simulate";

    public IReadOnlyList<string> Aliases => ["sim", "test", "mock"];

    public string Category => "category.stream";

    public string Description => CliText.Get("command.simulate.description");

    public string Usage => CliText.Get("command.simulate.usage");

    public async Task<int> ExecuteAsync(
        string triggerName,
        string[] args,
        CliExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (args.Length == 0)
        {
            return await context.FailAsync("UNKNOWN_COMMAND");
        }

        object body = args[0] == "chat"
            ? new { message = args.Length >= 2 ? string.Join(' ', args.Skip(1)) : "hello" }
            : new { };

        var response = await context.Client.PostAsJsonAsync(
            $"/api/simulate/{Uri.EscapeDataString(args[0])}",
            body,
            cancellationToken);
        return await context.WriteResponseAsync(response);
    }

    public IReadOnlyList<string> GetSuggestions(string[] args)
    {
        if (args.Length > 1)
        {
            return [];
        }

        var prefix = args.Length == 0 ? string.Empty : args[0];
        return new[] { "chat", "follow", "sub" }
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
    }
}

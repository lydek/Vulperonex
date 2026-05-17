using System.Net.Http.Json;

internal sealed class ConfigCommand : CompositeConsoleCommand
{
    public ConfigCommand()
    {
        AddSubCommand(new DelegateConsoleCommand("get", "Read a configuration value.", async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.GetAsync($"/api/config/{Uri.EscapeDataString(args[0])}", ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
        AddSubCommand(new DelegateConsoleCommand("set", "Write a configuration value.", async (args, context, ct) =>
            args.Length >= 2
                ? await context.WriteResponseAsync(await context.Client.PutAsJsonAsync(
                    $"/api/config/{Uri.EscapeDataString(args[0])}",
                    new { value = args[1] },
                    cancellationToken: ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
    }

    public override string Name => "config";

    public override string Description => "Read and write configuration.";
}

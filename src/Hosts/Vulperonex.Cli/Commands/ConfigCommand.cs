using System.Net.Http.Json;

internal sealed class ConfigCommand : CompositeConsoleCommand
{
    public ConfigCommand()
    {
        AddSubCommand(new DelegateConsoleCommand("get", "command.config.get.description", "command.config.get.usage", Category, async (args, context, ct) =>
            args.Length >= 1
                ? await context.WriteResponseAsync(await context.Client.GetAsync($"/api/config/{Uri.EscapeDataString(args[0])}", ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
        AddSubCommand(new DelegateConsoleCommand("set", "command.config.set.description", "command.config.set.usage", Category, async (args, context, ct) =>
            args.Length >= 2
                ? await context.WriteResponseAsync(await context.Client.PutAsJsonAsync(
                    $"/api/config/{Uri.EscapeDataString(args[0])}",
                    new { value = args[1] },
                    cancellationToken: ct))
                : await context.FailAsync("UNKNOWN_COMMAND")));
    }

    public override string Name => "config";

    public override IReadOnlyList<string> Aliases => ["cfg"];

    public override string Category => "category.system";

    public override string Description => CliText.Get("command.config.description");

    public override string Usage => CliText.Get("command.config.usage");
}

using System.Globalization;
using System.Text;
using System.Text.Json;

internal sealed class TimerCommand : CompositeConsoleCommand
{
    public TimerCommand()
    {
        AddSubCommand(new DelegateConsoleCommand("list", "command.timer.list.description", "command.timer.list.usage", Category, async (_, context, ct) =>
            await context.WriteResponseAsync(await context.Client.GetAsync("/api/timers", ct)), ["ls"]));
        AddSubCommand(new DelegateConsoleCommand("show", "command.timer.show.description", "command.timer.show.usage", Category, ShowAsync));
        AddSubCommand(new DelegateConsoleCommand("create", "command.timer.create.description", "command.timer.create.usage", Category, CreateAsync, ["add", "new"]));
        AddSubCommand(new DelegateConsoleCommand("delete", "command.timer.delete.description", "command.timer.delete.usage", Category, DeleteAsync));
    }

    public override string Name => "timer";

    public override IReadOnlyList<string> Aliases => ["t"];

    public override string Category => "category.workflow";

    public override string Description => CliText.Get("command.timer.description");

    public override string Usage => CliText.Get("command.timer.usage");

    private static async Task<int> ShowAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        if (args.Length < 1)
        {
            return await context.MissingArgsAsync("command.timer.show.usage", "command.timer.show.hint");
        }

        return await context.WriteResponseAsync(
            await context.Client.GetAsync($"/api/timers/{Uri.EscapeDataString(args[0])}", ct));
    }

    private static async Task<int> CreateAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        if (args.Length < 3)
        {
            return await context.MissingArgsAsync("command.timer.create.usage", "command.timer.create.hint");
        }

        if (!int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var intervalSeconds))
        {
            return await context.FailAsync("INTERVAL_SECONDS_INVALID");
        }

        if (!DateTimeOffset.TryParse(args[2], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var nextFireAt))
        {
            return await context.FailAsync("NEXT_FIRE_AT_INVALID");
        }

        var isEnabled = !args.Any(arg => string.Equals(arg, "--disabled", StringComparison.OrdinalIgnoreCase));
        var payload = JsonSerializer.Serialize(new
        {
            ruleId = args[0],
            intervalSeconds,
            isEnabled,
            nextFireAt,
        }, context.JsonOptions);
        using var body = new StringContent(payload, Encoding.UTF8, "application/json");

        return await context.WriteResponseAsync(await context.Client.PostAsync("/api/timers", body, ct));
    }

    private static async Task<int> DeleteAsync(string[] args, CliExecutionContext context, CancellationToken ct)
    {
        if (args.Length < 1)
        {
            return await context.MissingArgsAsync("command.timer.delete.usage", "command.timer.delete.hint");
        }

        var id = args[0];
        var yes = args.Any(arg => string.Equals(arg, "--yes", StringComparison.OrdinalIgnoreCase));
        if (!await context.ConfirmAsync(
            "command.timer.delete.confirm",
            [$"id: {id}"],
            yes))
        {
            return 1;
        }

        return await context.WriteResponseAsync(
            await context.Client.DeleteAsync($"/api/timers/{Uri.EscapeDataString(id)}", ct),
            $"OK timer deleted: {id}");
    }
}

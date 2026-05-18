using System.Net.Http.Json;

internal sealed class SimulateCommand : CompositeConsoleCommand
{
    public SimulateCommand()
    {
        AddSubCommand(new ChatCommand());
        AddSubCommand(new EventCommand("follow", "command.simulate.follow.description", "command.simulate.follow.usage"));
        AddSubCommand(new EventCommand("sub", "command.simulate.sub.description", "command.simulate.sub.usage"));
    }

    public override string Name => "simulate";

    public override IReadOnlyList<string> Aliases => ["sim", "test", "mock"];

    public override string Category => "category.stream";

    public override string Description => CliText.Get("command.simulate.description");

    public override string Usage => CliText.Get("command.simulate.usage");

    private sealed class ChatCommand : IConsoleCommand
    {
        public string Name => "chat";

        public IReadOnlyList<string> Aliases => ["message", "msg"];

        public string Category => "category.stream";

        public string Description => CliText.Get("command.simulate.chat.description");

        public string Usage => CliText.Get("command.simulate.chat.usage");

        public async Task<int> ExecuteAsync(
            string triggerName,
            string[] args,
            CliExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var options = SimulateOptions.Parse(args);
            if (options is null)
            {
                return await context.FailAsync("INVALID_ARGS");
            }

            var response = await context.Client.PostAsJsonAsync(
                "/api/simulate/chat",
                new
                {
                    platformUserId = options.PlatformUserId,
                    displayName = options.DisplayName,
                    message = string.IsNullOrWhiteSpace(options.Message) ? "hello" : options.Message,
                },
                cancellationToken);
            return await context.WriteResponseAsync(response);
        }

        public IReadOnlyList<string> GetSuggestions(string[] args) => SimulateOptions.GetSuggestions(args);
    }

    private sealed class EventCommand(
        string name,
        string descriptionKey,
        string usageKey) : IConsoleCommand
    {
        public string Name { get; } = name;

        public IReadOnlyList<string> Aliases => [];

        public string Category => "category.stream";

        public string Description => CliText.Get(descriptionKey);

        public string Usage => CliText.Get(usageKey);

        public async Task<int> ExecuteAsync(
            string triggerName,
            string[] args,
            CliExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var options = SimulateOptions.Parse(args);
            if (options is null)
            {
                return await context.FailAsync("INVALID_ARGS");
            }

            var response = await context.Client.PostAsJsonAsync(
                $"/api/simulate/{Uri.EscapeDataString(Name)}",
                new
                {
                    platformUserId = options.PlatformUserId,
                    displayName = options.DisplayName,
                    tier = options.Tier,
                },
                cancellationToken);
            return await context.WriteResponseAsync(response);
        }

        public IReadOnlyList<string> GetSuggestions(string[] args) => SimulateOptions.GetSuggestions(args);
    }

    private sealed record SimulateOptions(string? PlatformUserId, string? DisplayName, string? Tier, string Message)
    {
        public static SimulateOptions? Parse(string[] args)
        {
            string? userId = null;
            string? displayName = null;
            string? tier = null;
            var message = new List<string>();

            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                if (arg is "--user-id" or "--display-name" or "--tier")
                {
                    if (index + 1 >= args.Length)
                    {
                        return null;
                    }

                    var value = args[++index];
                    if (arg == "--user-id")
                    {
                        userId = value;
                    }
                    else if (arg == "--display-name")
                    {
                        displayName = value;
                    }
                    else
                    {
                        tier = value;
                    }

                    continue;
                }

                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    return null;
                }

                message.Add(arg);
            }

            return new SimulateOptions(userId, displayName, tier, string.Join(' ', message));
        }

        public static IReadOnlyList<string> GetSuggestions(string[] args)
        {
            var prefix = args.Length == 0 ? string.Empty : args[^1];
            return new[] { "--user-id", "--display-name", "--tier" }
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
    }
}

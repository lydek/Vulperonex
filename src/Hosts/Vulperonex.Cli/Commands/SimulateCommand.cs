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
        private const string DefaultChatMessage = "hello";

        private static readonly IReadOnlySet<string> AllowedFlags = new HashSet<string>(StringComparer.Ordinal)
        {
            "--user-id",
            "--display-name",
        };

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
            var options = SimulateOptions.Parse(args, AllowedFlags);
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
                    message = string.IsNullOrWhiteSpace(options.Message) ? DefaultChatMessage : options.Message,
                },
                cancellationToken);
            return await context.WriteResponseAsync(response, "OK simulated chat");
        }

        public IReadOnlyList<string> GetSuggestions(string[] args) => SimulateOptions.GetSuggestions(args, AllowedFlags);
    }

    private sealed class EventCommand(
        string name,
        string descriptionKey,
        string usageKey) : IConsoleCommand
    {
        private static readonly IReadOnlySet<string> AllowedFlags = new HashSet<string>(StringComparer.Ordinal)
        {
            "--user-id",
            "--display-name",
            "--tier",
        };

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
            var options = SimulateOptions.Parse(args, AllowedFlags);
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
            return await context.WriteResponseAsync(response, $"OK simulated {Name}");
        }

        public IReadOnlyList<string> GetSuggestions(string[] args) => SimulateOptions.GetSuggestions(args, AllowedFlags);
    }

    private sealed record SimulateOptions(string? PlatformUserId, string? DisplayName, string? Tier, string Message)
    {
        public static SimulateOptions? Parse(string[] args, IReadOnlySet<string> allowedFlags)
        {
            string? userId = null;
            string? displayName = null;
            string? tier = null;
            var message = new List<string>();

            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    if (!allowedFlags.Contains(arg))
                    {
                        return null;
                    }

                    if (index + 1 >= args.Length)
                    {
                        return null;
                    }

                    var value = args[++index];
                    switch (arg)
                    {
                        case "--user-id":
                            userId = value;
                            break;
                        case "--display-name":
                            displayName = value;
                            break;
                        case "--tier":
                            tier = value;
                            break;
                    }

                    continue;
                }

                message.Add(arg);
            }

            return new SimulateOptions(userId, displayName, tier, string.Join(' ', message));
        }

        public static IReadOnlyList<string> GetSuggestions(string[] args, IReadOnlySet<string> allowedFlags)
        {
            var prefix = args.Length == 0 ? string.Empty : args[^1];
            return allowedFlags
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
    }
}

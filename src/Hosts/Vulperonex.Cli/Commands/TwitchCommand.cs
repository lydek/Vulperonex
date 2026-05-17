using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

internal sealed class TwitchCommand : CompositeConsoleCommand
{
    public TwitchCommand()
    {
        var auth = new AuthCommand();
        AddSubCommand(auth);
    }

    public override string Name => "twitch";

    public override string Description => "Manage Twitch integration.";

    private sealed class AuthCommand : CompositeConsoleCommand
    {
        public AuthCommand()
        {
            AddSubCommand(new StartCommand());
        }

        public override string Name => "auth";

        public override string Description => "Manage Twitch OAuth authorization.";
    }

    private sealed class StartCommand : IConsoleCommand
    {
        public string Name => "start";

        public IReadOnlyList<string> Aliases => [];

        public string Description => "Start Twitch OAuth authorization.";

        public async Task<int> ExecuteAsync(
            string triggerName,
            string[] args,
            CliExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (context.IsInteractive)
            {
                var status = await new TwitchAuthStatusProbe(context.Client).ProbeAsync(cancellationToken);
                if (status.Succeeded && !status.ClientIdConfigured)
                {
                    await context.Error.WriteLineAsync("TWITCH_CLIENT_ID_MISSING");
                    await context.Error.WriteLineAsync("Set Twitch__ClientId and restart the Web host, or continue using non-Twitch commands in no-Twitch mode.");
                    return 1;
                }
            }

            var noBrowser = args.Contains("--no-browser", StringComparer.Ordinal);
            var callbackPort = CallbackPortSelector.Select();
            var startResponse = await context.Client.PostAsJsonAsync(
                "/api/twitch/auth/start",
                new { callbackPort },
                cancellationToken);
            if (!startResponse.IsSuccessStatusCode)
            {
                return await context.WriteResponseAsync(startResponse);
            }

            var payload = await startResponse.Content.ReadFromJsonAsync<TwitchAuthStartResponse>(
                context.JsonOptions,
                cancellationToken);
            if (payload is null)
            {
                return await context.FailAsync("INVALID_ACTION_CONFIG");
            }

            if (noBrowser)
            {
                await context.Output.WriteLineAsync(JsonSerializer.Serialize(payload, context.JsonOptions));
                return 0;
            }

            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{payload.CallbackPort}/auth/callback/");
            listener.Start();
            Process.Start(new ProcessStartInfo(payload.AuthorizeUrl) { UseShellExecute = true });
            await context.Output.WriteLineAsync(
                $"Opened Twitch authorization URL. Waiting on http://localhost:{payload.CallbackPort}/auth/callback");

            var callback = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(5), cancellationToken);
            var state = callback.Request.QueryString["state"];
            var code = callback.Request.QueryString["code"];
            await WriteCallbackResponseAsync(callback.Response, cancellationToken);

            var completeResponse = await context.Client.PostAsJsonAsync(
                "/api/twitch/auth/complete",
                new { state, code },
                cancellationToken);
            return await context.WriteResponseAsync(completeResponse);
        }

        public IReadOnlyList<string> GetSuggestions(string[] args)
        {
            return args.Length <= 1 && "--no-browser".StartsWith(args.Length == 0 ? string.Empty : args[0], StringComparison.Ordinal)
                ? ["--no-browser"]
                : [];
        }

        private static async Task WriteCallbackResponseAsync(HttpListenerResponse response, CancellationToken cancellationToken)
        {
            const string html = "<!doctype html><title>Vulperonex</title><p>Twitch authorization received. You can close this window.</p>";
            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, cancellationToken);
            response.Close();
        }
    }

    private sealed record TwitchAuthStartResponse(string AuthorizeUrl, string State, int CallbackPort);
}

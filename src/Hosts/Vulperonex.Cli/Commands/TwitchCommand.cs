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

    public override IReadOnlyList<string> Aliases => ["tw"];

    public override string Category => "category.integration";

    public override string Description => CliText.Get("command.twitch.description");

    public override string Usage => CliText.Get("command.twitch.usage");

    private sealed class AuthCommand : CompositeConsoleCommand
    {
        public AuthCommand()
        {
            AddSubCommand(new StartCommand());
            AddSubCommand(new DelegateConsoleCommand("reset", "command.twitch.auth.reset.description", "command.twitch.auth.reset.usage", Category, async (_, context, ct) =>
                await context.WriteResponseAsync(await context.Client.DeleteAsync("/api/twitch/auth/token", ct)), ["clear", "logout", "remove"]));
        }

        public override string Name => "auth";

        public override string Category => "category.integration";

        public override string Description => CliText.Get("command.twitch.auth.description");

        public override string Usage => CliText.Get("command.twitch.auth.usage");
    }

    private sealed class StartCommand : IConsoleCommand
    {
        public string Name => "start";

        public IReadOnlyList<string> Aliases => [];

        public string Category => "category.integration";

        public string Description => CliText.Get("command.twitch.auth.start.description");

        public string Usage => CliText.Get("command.twitch.auth.start.usage");

        public async Task<int> ExecuteAsync(
            string triggerName,
            string[] args,
            CliExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteCoreAsync(args, context, cancellationToken);
            }
            catch (OperationCanceledException) when (context.IsInteractive && cancellationToken.IsCancellationRequested)
            {
                await context.Error.WriteLineAsync("TWITCH_OAUTH_CANCELLED");
                return 1;
            }
        }

        private static async Task<int> ExecuteCoreAsync(
            string[] args,
            CliExecutionContext context,
            CancellationToken cancellationToken)
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

            var authStatus = await new TwitchAuthStatusProbe(context.Client).ProbeAsync(cancellationToken);
            var noBrowser = args.Contains("--no-browser", StringComparer.Ordinal);
            if (authStatus.Succeeded && authStatus.ClientIdConfigured && !authStatus.ClientSecretConfigured)
            {
                return await RunDeviceAuthorizationAsync(context, noBrowser, cancellationToken);
            }

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

            var completeResponse = await context.Client.PostAsJsonAsync(
                "/api/twitch/auth/complete",
                new { state, code },
                cancellationToken);
            await WriteCallbackResponseAsync(callback.Response, completeResponse.IsSuccessStatusCode, cancellationToken);
            return await context.WriteResponseAsync(completeResponse);
        }

        public IReadOnlyList<string> GetSuggestions(string[] args)
        {
            return args.Length <= 1 && "--no-browser".StartsWith(args.Length == 0 ? string.Empty : args[0], StringComparison.Ordinal)
                ? ["--no-browser"]
                : [];
        }

        private static async Task<int> RunDeviceAuthorizationAsync(
            CliExecutionContext context,
            bool noBrowser,
            CancellationToken cancellationToken)
        {
            var startResponse = await context.Client.PostAsJsonAsync("/api/twitch/auth/device/start", new { }, cancellationToken);
            if (!startResponse.IsSuccessStatusCode)
            {
                return await context.WriteResponseAsync(startResponse);
            }

            var payload = await startResponse.Content.ReadFromJsonAsync<TwitchDeviceAuthorizationResponse>(
                context.JsonOptions,
                cancellationToken);
            if (payload is null)
            {
                return await context.FailAsync("INVALID_ACTION_CONFIG");
            }

            await context.Output.WriteLineAsync("Twitch public-client authorization");
            await context.Output.WriteLineAsync($"Open: {payload.VerificationUri}");
            await context.Output.WriteLineAsync($"Code: {payload.UserCode}");
            if (!noBrowser)
            {
                Process.Start(new ProcessStartInfo(payload.VerificationUri) { UseShellExecute = true });
            }

            var deadline = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn);
            var interval = TimeSpan.FromSeconds(Math.Max(payload.Interval, 1));
            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(interval, cancellationToken);
                var completeResponse = await context.Client.PostAsJsonAsync(
                    "/api/twitch/auth/device/complete",
                    new { payload.DeviceCode },
                    cancellationToken);
                if (completeResponse.StatusCode == HttpStatusCode.Accepted)
                {
                    continue;
                }

                if (!completeResponse.IsSuccessStatusCode)
                {
                    return await context.WriteResponseAsync(completeResponse);
                }

                await context.Output.WriteLineAsync("Twitch authorization completed.");
                return 0;
            }

            return await context.FailAsync("TWITCH_OAUTH_EXCHANGE_FAILED");
        }

        private static async Task WriteCallbackResponseAsync(
            HttpListenerResponse response,
            bool authorizationCompleted,
            CancellationToken cancellationToken)
        {
            var html = authorizationCompleted
                ? """
                  <!doctype html><html lang="en"><head><meta charset="utf-8"><title>Vulperonex Twitch Authorization</title><style>body{font-family:Segoe UI,Arial,sans-serif;margin:48px;color:#1f2937}.status{max-width:560px}.ok{color:#047857}.fail{color:#b91c1c}</style></head><body><main class="status"><h1 class="ok">Twitch authorization completed</h1><p>You can close this browser tab and return to Vulperonex CLI.</p></main></body></html>
                  """
                : """
                  <!doctype html><html lang="en"><head><meta charset="utf-8"><title>Vulperonex Twitch Authorization</title><style>body{font-family:Segoe UI,Arial,sans-serif;margin:48px;color:#1f2937}.status{max-width:560px}.fail{color:#b91c1c}</style></head><body><main class="status"><h1 class="fail">Twitch authorization could not be completed</h1><p>Return to Vulperonex CLI for the error code, then retry after fixing the configuration.</p></main></body></html>
                  """;
            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, cancellationToken);
            response.Close();
        }
    }

    private sealed record TwitchAuthStartResponse(string AuthorizeUrl, string State, int CallbackPort);
    private sealed record TwitchDeviceAuthorizationResponse(
        string DeviceCode,
        string UserCode,
        string VerificationUri,
        int ExpiresIn,
        int Interval);
}

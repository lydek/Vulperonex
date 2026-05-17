internal sealed class InteractiveSession(
    ICommandDispatcher dispatcher,
    CliExecutionContext context,
    TextReader input)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        await WriteWelcomeAsync();
        await WriteTwitchStatusBannerAsync(cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await context.Output.WriteAsync("vulperonex> ");
            var line = ShouldUseLineEditor()
                ? await new LineEditor(dispatcher, context.Output).ReadLineAsync(cancellationToken)
                : await input.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                await context.Output.WriteLineAsync();
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

            try
            {
                await dispatcher.DispatchAsync(args, context, cancellationToken);
            }
            catch (HttpRequestException)
            {
                await context.Error.WriteLineAsync("HTTP_REQUEST_FAILED");
            }
        }
    }

    private bool ShouldUseLineEditor()
    {
        return ReferenceEquals(input, Console.In) && !Console.IsInputRedirected;
    }

    private async Task WriteWelcomeAsync()
    {
        await context.Output.WriteLineAsync("Vulperonex CLI interactive mode");
        await context.Output.WriteLineAsync($"API: {context.Client.BaseAddress}");
        await context.Output.WriteLineAsync("Type 'help' for commands, 'exit' to quit.");
    }

    private async Task WriteTwitchStatusBannerAsync(CancellationToken cancellationToken)
    {
        TwitchAuthStatusProbeResult status;
        try
        {
            status = await new TwitchAuthStatusProbe(context.Client).ProbeAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            await context.Error.WriteLineAsync("[WARN] Unable to read Twitch status (HTTP_REQUEST_FAILED). Continuing in no-Twitch mode.");
            return;
        }
        catch (TaskCanceledException)
        {
            await context.Error.WriteLineAsync("[WARN] Unable to read Twitch status (TIMEOUT). Continuing in no-Twitch mode.");
            return;
        }

        if (!status.Succeeded)
        {
            await context.Error.WriteLineAsync($"[WARN] Unable to read Twitch status ({status.ErrorCode}). Continuing in no-Twitch mode.");
            return;
        }

        if (!status.ClientIdConfigured)
        {
            await context.Error.WriteLineAsync("[WARN] Twitch:ClientId is not set. Continuing in no-Twitch mode. Set Twitch__ClientId and restart the Web host to enable Twitch auth.");
            return;
        }

        if (!status.HasRefreshToken)
        {
            await context.Error.WriteLineAsync("[WARN] Twitch OAuth is not authorized. Run 'twitch auth start' or 'twitch auth start --no-browser' to authorize.");
        }
    }
}

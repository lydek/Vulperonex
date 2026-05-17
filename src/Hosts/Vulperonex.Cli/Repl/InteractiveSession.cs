internal sealed class InteractiveSession(
    ICommandDispatcher dispatcher,
    CliExecutionContext context,
    TextReader input)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        await WriteTwitchStatusBannerAsync(cancellationToken);

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

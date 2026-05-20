using System.Text.Json;

var exitCode = await VulperonexCli.RunAsync(args);
return exitCode;

public static class VulperonexCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task<int> RunAsync(
        string[] args,
        HttpClient? client = null,
        TextWriter? output = null,
        TextWriter? error = null,
        TextReader? input = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;

        try
        {
            using var ownedClient = client is null ? CreateClient() : null;
            client ??= ownedClient!;
            var resolvedInput = input ?? Console.In;
            var dispatcher = CommandTreeFactory.Create();
            if (args.Length == 0)
            {
                var interactiveContext = new CliExecutionContext(client, output, error, resolvedInput, JsonOptions, isInteractive: true);
                return await new InteractiveSession(dispatcher, interactiveContext, resolvedInput).RunAsync();
            }

            if (args[0] is "--interactive" or "-i")
            {
                if (args.Length > 1)
                {
                    await error.WriteLineAsync("INVALID_ARGS");
                    return 1;
                }

                var interactiveContext = new CliExecutionContext(client, output, error, resolvedInput, JsonOptions, isInteractive: true);
                return await new InteractiveSession(dispatcher, interactiveContext, resolvedInput).RunAsync();
            }

            var context = new CliExecutionContext(client, output, error, resolvedInput, JsonOptions);
            return await dispatcher.DispatchAsync(args, context);
        }
        catch (HttpRequestException)
        {
            await error.WriteLineAsync("HTTP_REQUEST_FAILED");
            return 1;
        }
        catch (CliApiUrlNotLoopbackException)
        {
            await error.WriteLineAsync("CLI_API_URL_NOT_LOOPBACK");
            return 1;
        }
    }

    private static HttpClient CreateClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("VULPERONEX_API_URL") ?? "http://localhost:5000";
        var uri = new Uri(baseUrl);
        if (!IsAllowedLoopbackBaseUrl(uri))
        {
            throw new CliApiUrlNotLoopbackException();
        }

        return new HttpClient { BaseAddress = uri };
    }

    private static bool IsAllowedLoopbackBaseUrl(Uri uri)
    {
        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && (uri.IsLoopback
                || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CliApiUrlNotLoopbackException : Exception;
}

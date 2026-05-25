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

        var handler = new AdminGuardHttpClientHandler(new HttpClientHandler(), uri);
        return new HttpClient(handler) { BaseAddress = uri };
    }

    private static bool IsAllowedLoopbackBaseUrl(Uri uri)
    {
        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && (uri.IsLoopback
                || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CliApiUrlNotLoopbackException : Exception;

    private sealed class AdminGuardHttpClientHandler : DelegatingHandler
    {
        private string? _token;
        private readonly Uri _baseAddress;

        public AdminGuardHttpClientHandler(HttpMessageHandler innerHandler, Uri baseAddress) 
            : base(innerHandler)
        {
            _baseAddress = baseAddress;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.OriginalString;
            if (path == null && request.RequestUri != null)
            {
                path = request.RequestUri.ToString();
            }

            if (path != null && (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || path.Contains("/api/")))
            {
                var address = _baseAddress.ToString().TrimEnd('/');
                if (!request.Headers.Contains("Origin"))
                {
                    request.Headers.Add("Origin", address);
                }
                if (!request.Headers.Contains("Referer"))
                {
                    request.Headers.Add("Referer", address);
                }

                var method = request.Method;
                bool isGetCsrfToken = path.EndsWith("/api/overlay/csrf-token", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("/api/overlay/csrf-token");
                
                bool shouldProtect = !isGetCsrfToken && 
                    ((path.Contains("/api/overlay/") || path.StartsWith("/api/overlay/", StringComparison.OrdinalIgnoreCase))
                    || (path.Contains("/api/") && method != HttpMethod.Get));

                if (shouldProtect && !request.Headers.Contains("X-Admin-Csrf"))
                {
                    if (_token == null)
                    {
                        using var tokenClient = new HttpClient { BaseAddress = _baseAddress };
                        tokenClient.DefaultRequestHeaders.Add("Origin", address);
                        tokenClient.DefaultRequestHeaders.Add("Referer", address);

                        try
                        {
                            var tokenResponse = await tokenClient.GetAsync("/api/overlay/csrf-token", cancellationToken).ConfigureAwait(false);
                            if (tokenResponse.IsSuccessStatusCode)
                            {
                                var json = await tokenResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                                using var doc = JsonDocument.Parse(json);
                                if (doc.RootElement.TryGetProperty("token", out var tokenProp))
                                {
                                    _token = tokenProp.GetString();
                                }
                            }
                        }
                        catch
                        {
                            // Ignore token fetch exception and let the middleware deny it with 400
                        }
                    }

                    if (_token != null)
                    {
                        request.Headers.Add("X-Admin-Csrf", _token);
                    }
                }
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}

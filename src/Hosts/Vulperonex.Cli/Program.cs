using System.Net.Http.Json;
using System.Text.Json;

var exitCode = await VulperonexCli.RunAsync(args);
return exitCode;

public static class VulperonexCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task<int> RunAsync(string[] args, HttpClient? client = null, TextWriter? output = null, TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;

        if (args.Length == 0)
        {
            await error.WriteLineAsync("UNKNOWN_COMMAND");
            return 1;
        }

        try
        {
            using var ownedClient = client is null ? CreateClient() : null;
            client ??= ownedClient!;
            return await DispatchAsync(args, client, output, error);
        }
        catch (HttpRequestException)
        {
            await error.WriteLineAsync("HTTP_REQUEST_FAILED");
            return 1;
        }
        catch (InvalidOperationException exception) when (exception.Message == "CLI_API_URL_NOT_LOOPBACK")
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
            throw new InvalidOperationException("CLI_API_URL_NOT_LOOPBACK");
        }

        return new HttpClient { BaseAddress = uri };
    }

    private static bool IsAllowedLoopbackBaseUrl(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp
            && (uri.IsLoopback
                || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<int> DispatchAsync(string[] args, HttpClient client, TextWriter output, TextWriter error)
    {
        return args[0] switch
        {
            "rule" => await RuleAsync(args.Skip(1).ToArray(), client, output, error),
            "config" => await ConfigAsync(args.Skip(1).ToArray(), client, output, error),
            "member" => await MemberAsync(args.Skip(1).ToArray(), client, output, error),
            "simulate" => await SimulateAsync(args.Skip(1).ToArray(), client, error),
            _ => await FailAsync(error, "UNKNOWN_COMMAND"),
        };
    }

    private static async Task<int> RuleAsync(string[] args, HttpClient client, TextWriter output, TextWriter error)
    {
        if (args.Length == 0)
        {
            return await FailAsync(error, "UNKNOWN_COMMAND");
        }

        var response = args[0] switch
        {
            "list" => await client.GetAsync("/api/rules"),
            "show" when args.Length >= 2 => await client.GetAsync($"/api/rules/{Uri.EscapeDataString(args[1])}"),
            "enable" when args.Length >= 2 => await client.PostAsync($"/api/rules/{Uri.EscapeDataString(args[1])}/enable", null),
            "disable" when args.Length >= 2 => await client.PostAsync($"/api/rules/{Uri.EscapeDataString(args[1])}/disable", null),
            "delete" when args.Length >= 2 => await client.DeleteAsync($"/api/rules/{Uri.EscapeDataString(args[1])}"),
            _ => null,
        };

        return response is null
            ? await FailAsync(error, "UNKNOWN_COMMAND")
            : await WriteResponseAsync(response, output, error);
    }

    private static async Task<int> ConfigAsync(string[] args, HttpClient client, TextWriter output, TextWriter error)
    {
        if (args.Length < 2)
        {
            return await FailAsync(error, "UNKNOWN_COMMAND");
        }

        var key = Uri.EscapeDataString(args[1]);
        var response = args[0] switch
        {
            "get" => await client.GetAsync($"/api/config/{key}"),
            "set" when args.Length >= 3 => await client.PutAsJsonAsync($"/api/config/{key}", new { value = args[2] }),
            _ => null,
        };

        return response is null
            ? await FailAsync(error, "UNKNOWN_COMMAND")
            : await WriteResponseAsync(response, output, error);
    }

    private static async Task<int> MemberAsync(string[] args, HttpClient client, TextWriter output, TextWriter error)
    {
        if (args.Length == 0)
        {
            return await FailAsync(error, "UNKNOWN_COMMAND");
        }

        var response = args[0] switch
        {
            "list" => await client.GetAsync("/api/members"),
            "show" when args.Length >= 2 => await client.GetAsync($"/api/members/{Uri.EscapeDataString(args[1])}"),
            _ => null,
        };

        return response is null
            ? await FailAsync(error, "UNKNOWN_COMMAND")
            : await WriteResponseAsync(response, output, error);
    }

    private static async Task<int> SimulateAsync(string[] args, HttpClient client, TextWriter error)
    {
        if (args.Length == 0)
        {
            return await FailAsync(error, "UNKNOWN_COMMAND");
        }

        object body = args[0] == "chat"
            ? new { message = args.Length >= 2 ? string.Join(' ', args.Skip(1)) : "hello" }
            : new { };

        var response = await client.PostAsJsonAsync($"/api/simulate/{Uri.EscapeDataString(args[0])}", body);
        return await WriteResponseAsync(response, TextWriter.Null, error);
    }

    private static async Task<int> WriteResponseAsync(HttpResponseMessage response, TextWriter output, TextWriter error)
    {
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync();
            var errorCode = TryReadErrorCode(payload) ?? response.StatusCode.ToString();
            await error.WriteLineAsync(errorCode);
            return 1;
        }

        if (response.Content.Headers.ContentLength is not 0)
        {
            var payload = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(payload))
            {
                var json = JsonSerializer.Deserialize<JsonElement>(payload, JsonOptions);
                await output.WriteLineAsync(JsonSerializer.Serialize(json, JsonOptions));
            }
        }

        return 0;
    }

    private static string? TryReadErrorCode(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("error", out var error)
            ? error.GetString()
            : null;
    }

    private static async Task<int> FailAsync(TextWriter error, string code)
    {
        await error.WriteLineAsync(code);
        return 1;
    }
}

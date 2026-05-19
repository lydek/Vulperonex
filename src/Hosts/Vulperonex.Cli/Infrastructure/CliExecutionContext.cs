using System.Text.Json;
using System.Net;

internal sealed class CliExecutionContext(
    HttpClient client,
    TextWriter output,
    TextWriter error,
    JsonSerializerOptions jsonOptions,
    bool isInteractive = false)
{
    public HttpClient Client { get; } = client;

    public TextWriter Output { get; } = output;

    public TextWriter Error { get; } = error;

    public JsonSerializerOptions JsonOptions { get; } = jsonOptions;

    public bool IsInteractive { get; } = isInteractive;

    public Task<int> WriteResponseAsync(HttpResponseMessage response)
    {
        return WriteResponseAsync(response, null);
    }

    public async Task<int> WriteResponseAsync(HttpResponseMessage response, string? successMessage)
    {
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync();
            var errorCode = TryReadErrorCode(payload) ?? response.StatusCode.ToString();
            await Error.WriteLineAsync(errorCode);
            return 1;
        }

        var wrotePayload = false;
        if (response.Content.Headers.ContentLength is not 0)
        {
            var payload = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(payload))
            {
                var json = JsonSerializer.Deserialize<JsonElement>(payload, JsonOptions);
                await Output.WriteLineAsync(JsonSerializer.Serialize(json, JsonOptions));
                wrotePayload = true;
            }
        }

        if (!wrotePayload)
        {
            await Output.WriteLineAsync(successMessage ?? SuccessMessage(response.StatusCode));
        }

        return 0;
    }

    public async Task<int> FailAsync(string code)
    {
        await Error.WriteLineAsync(code);
        return 1;
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

    private static string SuccessMessage(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Accepted => "OK ACCEPTED",
            HttpStatusCode.NoContent => "OK NO_CONTENT",
            _ => $"OK {(int)statusCode}",
        };
    }
}

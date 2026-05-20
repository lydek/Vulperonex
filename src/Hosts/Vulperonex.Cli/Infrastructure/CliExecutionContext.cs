using System.Text.Json;
using System.Net;

internal sealed class CliExecutionContext(
    HttpClient client,
    TextWriter output,
    TextWriter error,
    TextReader input,
    JsonSerializerOptions jsonOptions,
    bool isInteractive = false)
{
    public HttpClient Client { get; } = client;

    public TextWriter Output { get; } = output;

    public TextWriter Error { get; } = error;

    public TextReader Input { get; } = input;

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

    public async Task<int> MissingArgsAsync(string usageKey, string hintKey)
    {
        await Error.WriteLineAsync("MISSING_ARGS");
        await Error.WriteLineAsync($"usage: {CliText.Get(usageKey)}");
        await Error.WriteLineAsync($"hint: {CliText.Get(hintKey)}");
        return 1;
    }

    /// <summary>
    /// Prompts the user to confirm a destructive operation. Returns true if the operation should proceed.
    /// In interactive mode, reads a line from <see cref="Input"/>; "y"/"yes" (case-insensitive) confirms.
    /// In non-interactive mode, requires <paramref name="hasYesFlag"/>; otherwise writes
    /// CONFIRMATION_REQUIRED and returns false.
    /// </summary>
    public async Task<bool> ConfirmAsync(string confirmMessageKey, IReadOnlyList<string> summaryLines, bool hasYesFlag)
    {
        if (hasYesFlag)
        {
            return true;
        }

        if (!IsInteractive)
        {
            await Error.WriteLineAsync("CONFIRMATION_REQUIRED");
            await Error.WriteLineAsync(CliText.Get(confirmMessageKey));
            foreach (var line in summaryLines)
            {
                await Error.WriteLineAsync($"  {line}");
            }

            await Error.WriteLineAsync($"hint: {CliText.Get("resolver.confirm.yes-flag-hint")}");
            return false;
        }

        await Output.WriteLineAsync(CliText.Get(confirmMessageKey));
        foreach (var line in summaryLines)
        {
            await Output.WriteLineAsync($"  {line}");
        }

        await Output.WriteAsync(CliText.Get("resolver.confirm.prompt"));
        await Output.FlushAsync();

        var response = await Input.ReadLineAsync();
        if (response is null)
        {
            await Error.WriteLineAsync("CANCELLED");
            return false;
        }

        var trimmed = response.Trim();
        if (string.Equals(trimmed, "y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        await Error.WriteLineAsync("CANCELLED");
        return false;
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

internal sealed class LineEditor(
    ICommandDispatcher dispatcher,
    TextWriter output,
    IList<string>? history = null)
{
    private readonly IList<string> _history = history ?? [];

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var buffer = string.Empty;
        var historyIndex = _history.Count;
        CompletionCycle? completionCycle = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Console.ReadKey(intercept: true);

            if (key.KeyChar == '\x03')
            {
                await output.WriteLineAsync("^C");
                return string.Empty;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                await output.WriteLineAsync();
                if (!string.IsNullOrWhiteSpace(buffer))
                {
                    if (_history.Count == 0 || !string.Equals(_history[^1], buffer, StringComparison.Ordinal))
                    {
                        _history.Add(buffer);
                    }
                }

                return buffer;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                completionCycle = null;
                if (buffer.Length > 0)
                {
                    buffer = buffer[..^1];
                    await output.WriteAsync("\b \b");
                }

                continue;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                completionCycle ??= CommandCompletion.StartCycle(buffer, dispatcher);
                var completed = CommandCompletion.ApplyCycle(completionCycle, dispatcher, out completionCycle);
                if (!string.Equals(completed, buffer, StringComparison.Ordinal))
                {
                    await ReplaceCurrentLineAsync(buffer, completed);
                    buffer = completed;
                }

                continue;
            }

            if (key.Key == ConsoleKey.UpArrow)
            {
                completionCycle = null;
                if (_history.Count == 0 || historyIndex == 0)
                {
                    continue;
                }

                historyIndex--;
                var entry = _history[historyIndex];
                await ReplaceCurrentLineAsync(buffer, entry);
                buffer = entry;
                continue;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                completionCycle = null;
                if (_history.Count == 0 || historyIndex >= _history.Count)
                {
                    continue;
                }

                historyIndex++;
                var entry = historyIndex == _history.Count ? string.Empty : _history[historyIndex];
                await ReplaceCurrentLineAsync(buffer, entry);
                buffer = entry;
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                completionCycle = null;
                buffer += key.KeyChar;
                await output.WriteAsync(key.KeyChar);
            }
        }
    }

    private async Task ReplaceCurrentLineAsync(string current, string replacement)
    {
        await output.WriteAsync(new string('\b', current.Length));
        await output.WriteAsync(new string(' ', current.Length));
        await output.WriteAsync(new string('\b', current.Length));
        await output.WriteAsync(replacement);
    }
}

internal sealed class LineEditor(ICommandDispatcher dispatcher, TextWriter output)
{
    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var buffer = string.Empty;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                await output.WriteLineAsync();
                return buffer;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer = buffer[..^1];
                    await output.WriteAsync("\b \b");
                }

                continue;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                var completed = CommandCompletion.Complete(buffer, dispatcher);
                if (!string.Equals(completed, buffer, StringComparison.Ordinal))
                {
                    await ReplaceCurrentLineAsync(buffer, completed);
                    buffer = completed;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
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

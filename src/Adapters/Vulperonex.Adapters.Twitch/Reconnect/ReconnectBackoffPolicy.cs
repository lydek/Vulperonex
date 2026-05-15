namespace Vulperonex.Adapters.Twitch.Reconnect;

public sealed class ReconnectBackoffPolicy(double jitterFactor)
{
    public ReconnectBackoffPolicy()
        : this(jitterFactor: 0)
    {
    }

    public TimeSpan GetDelay(int attempt)
    {
        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt), "Attempt must be greater than zero.");
        }

        var baseSeconds = Math.Min(Math.Pow(2, attempt - 1), 60);
        var jitter = Math.Clamp(jitterFactor, -0.2, 0.2);
        return TimeSpan.FromSeconds(Math.Min(baseSeconds * (1 + jitter), 60));
    }
}

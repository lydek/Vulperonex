namespace Vulperonex.Adapters.Abstractions;

public interface IStreamEventSource
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

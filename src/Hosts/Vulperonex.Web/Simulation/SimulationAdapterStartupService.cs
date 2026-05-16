using Vulperonex.Adapters.Simulation;

namespace Vulperonex.Web.Simulation;

public sealed class SimulationAdapterStartupService(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var adapter = scope.ServiceProvider.GetRequiredService<ISimulationAdapter>();
        await adapter.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

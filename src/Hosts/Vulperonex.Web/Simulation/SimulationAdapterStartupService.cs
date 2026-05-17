using Microsoft.Extensions.Configuration;
using Vulperonex.Adapters.Simulation;

namespace Vulperonex.Web.Simulation;

public sealed class SimulationAdapterStartupService(IServiceProvider serviceProvider, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Simulation:Enabled", true))
        {
            return;
        }

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

using Vulperonex.Application.Workflows.Actions;

namespace Vulperonex.Web.Simulation;

public sealed class SimulationPlatformChatSender : IPlatformChatSender
{
    public string Platform => "simulation";

    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

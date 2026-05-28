using Microsoft.Extensions.Hosting;
using Vulperonex.Application.EventTypes;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows.Timers;

public sealed class WorkflowInternalEventTypeBootstrapper(IStreamEventTypeRegistry registry) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        registry.Register(new StreamEventTypeMetadata(
            StreamEventKeys.WorkflowTimer,
            "Timer fired",
            IsSystemEvent: false));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

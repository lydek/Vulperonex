using Vulperonex.Application.EventBus;
using Vulperonex.Application.Members;

namespace Vulperonex.Web.Members;

public sealed class MemberModuleHostedService(IServiceProvider serviceProvider) : IHostedService, IAsyncDisposable
{
    private AsyncServiceScope? _scope;
    private MemberModule? _module;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _scope = serviceProvider.CreateAsyncScope();
        var services = _scope.Value.ServiceProvider;
        _module = new MemberModule(
            services.GetRequiredService<IStreamEventBus>(),
            services.GetRequiredService<IMemberResolver>(),
            services.GetRequiredService<IMemberStreamStateRepository>(),
            services.GetRequiredService<TimeProvider>());
        await _module.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            await _module.StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _module?.Dispose();
        if (_scope is not null)
        {
            await _scope.Value.DisposeAsync();
        }
    }
}

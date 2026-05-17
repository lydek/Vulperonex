using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Web;

public sealed class DatabaseMigrationStartupService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<DatabaseBootstrapper>();
        await bootstrapper.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

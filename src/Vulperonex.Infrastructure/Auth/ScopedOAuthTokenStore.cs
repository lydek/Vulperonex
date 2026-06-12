using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Application.Auth;

namespace Vulperonex.Infrastructure.Auth;

/// <summary>
/// Scope-bridging <see cref="IOAuthTokenStore"/> for singleton consumers
/// (the Twitch access-token provider). The EF-backed store is scoped because
/// it owns a DbContext; this bridge opens a short-lived scope per call so the
/// token provider can live for the process lifetime, keep its access token
/// cached, and own the single refresh path — concurrent scoped providers used
/// to race the rotating refresh token and wipe a freshly issued one.
/// </summary>
public sealed class ScopedOAuthTokenStore(IServiceScopeFactory scopeFactory) : IOAuthTokenStore
{
    public async Task StoreRefreshTokenAsync(string platform, string rawToken, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<IOAuthTokenStore>()
            .StoreRefreshTokenAsync(platform, rawToken, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> GetRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IOAuthTokenStore>()
            .GetRefreshTokenAsync(platform, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IOAuthTokenStore>()
            .HasRefreshTokenAsync(platform, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ClearRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<IOAuthTokenStore>()
            .ClearRefreshTokenAsync(platform, cancellationToken)
            .ConfigureAwait(false);
    }
}

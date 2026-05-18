namespace Vulperonex.Application.Auth;

public interface IOAuthTokenStore
{
    Task StoreRefreshTokenAsync(string platform, string rawToken, CancellationToken cancellationToken = default);

    Task<string?> GetRefreshTokenAsync(string platform, CancellationToken cancellationToken = default);

    Task<bool> HasRefreshTokenAsync(string platform, CancellationToken cancellationToken = default);

    Task ClearRefreshTokenAsync(string platform, CancellationToken cancellationToken = default);
}

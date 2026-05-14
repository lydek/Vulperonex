namespace Vulperonex.Application.Settings;

public interface ISystemSettingsService
{
    Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, string category, CancellationToken cancellationToken = default);
}

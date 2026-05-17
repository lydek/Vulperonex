using Vulperonex.Application.Auth;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Security;

namespace Vulperonex.Infrastructure.Auth;

public sealed class OAuthTokenStore(
    ISystemSettingsService settings,
    MachineKeyProvider machineKeyProvider) : IOAuthTokenStore
{
    private const string TwitchPlatform = "twitch";
    public async Task StoreRefreshTokenAsync(
        string platform,
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        var settingKey = SettingKeyForPlatform(platform);
        var key = await machineKeyProvider.GetKeyAsync(cancellationToken);
        var encrypted = OAuthTokenEnvelope.Encrypt(rawToken, key, settingKey);
        await settings.SetAsync(settingKey, encrypted, "oauth", cancellationToken);
    }

    public async Task<string?> GetRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
    {
        var settingKey = SettingKeyForPlatform(platform);
        try
        {
            var encrypted = await settings.GetAsync<string?>(settingKey, null, cancellationToken);
            if (encrypted is null)
            {
                return null;
            }

            var key = await machineKeyProvider.GetKeyAsync(cancellationToken);
            return OAuthTokenEnvelope.Decrypt(encrypted, key, settingKey);
        }
        catch (Exception exception) when (exception is CredentialDecryptionException or IOException or System.Text.Json.JsonException)
        {
            throw new CredentialDecryptionException("OAuth credential could not be decrypted.", exception);
        }
    }

    public async Task<bool> HasRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
    {
        return !string.IsNullOrWhiteSpace(await GetRefreshTokenAsync(platform, cancellationToken));
    }

    private static string SettingKeyForPlatform(string platform)
    {
        if (!string.Equals(platform, TwitchPlatform, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unknown OAuth platform: {platform}");
        }

        return SystemSettingKey.OAuthTwitchRefreshToken;
    }
}

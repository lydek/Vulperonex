using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Vulperonex.Web.Security;

/// <summary>
/// Singleton holder for the overlay LAN access key.
///
/// [Security Design]:
/// When cross-machine overlay access is enabled (<c>Overlay:Lan:Enabled</c>), live overlay
/// requests originating from non-loopback (LAN) addresses must carry a valid access key
/// (<c>?k=</c> query or <c>X-Overlay-Key</c> header). This prevents arbitrary devices on the
/// same network segment from subscribing to the event stream (chat, donations, member data).
///
/// Unlike the admin CSRF token, this key is persisted in <c>SystemSettings</c> and therefore
/// remains stable across application restarts — OBS browser sources keep working without
/// reconfiguring the URL. The key is generated once on first enable and cached here in memory
/// so per-request validation does not touch the database.
/// </summary>
public sealed class OverlayLanAccessKeyProvider
{
    private volatile string? _key;

    /// <summary>Current access key, or <c>null</c> when LAN access is disabled / not yet seeded.</summary>
    public string? Key => _key;

    public void SetKey(string? key) => _key = key;

    /// <summary>Generates a fresh URL-safe random key (256-bit).</summary>
    public static string GenerateKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    /// <summary>Constant-time comparison of a candidate against the active key.</summary>
    public bool Validate(string? candidate)
    {
        var key = _key;
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(candidate),
            System.Text.Encoding.UTF8.GetBytes(key));
    }
}

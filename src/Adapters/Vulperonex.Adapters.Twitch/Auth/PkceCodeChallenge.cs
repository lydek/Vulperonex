using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Vulperonex.Adapters.Twitch.Auth;

public static partial class PkceCodeChallenge
{
    public static string CreateVerifier()
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    public static string FromVerifier(string verifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifier);
        if (verifier.Length is < 43 or > 128 || !VerifierRegex().IsMatch(verifier))
        {
            throw new ArgumentException("PKCE code verifier contains invalid characters.", nameof(verifier));
        }

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    [GeneratedRegex("^[A-Za-z0-9._~-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex VerifierRegex();
}

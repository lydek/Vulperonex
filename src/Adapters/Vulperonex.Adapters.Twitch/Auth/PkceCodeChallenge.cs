using System.Security.Cryptography;
using System.Text;

namespace Vulperonex.Adapters.Twitch.Auth;

public static class PkceCodeChallenge
{
    public static string CreateVerifier()
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    public static string FromVerifier(string verifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifier);

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

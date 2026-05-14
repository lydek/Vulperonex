using System.Security.Cryptography;
using System.Text;
using Vulperonex.Application.Auth;

namespace Vulperonex.Infrastructure.Auth;

public static class OAuthTokenEnvelope
{
    private const string VersionPrefix = "v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static string Encrypt(string rawToken, byte[] key, string settingKey)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintext = Encoding.UTF8.GetBytes(rawToken);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        var aad = Encoding.UTF8.GetBytes(settingKey);

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

        return VersionPrefix + Convert.ToBase64String([.. nonce, .. ciphertext, .. tag]);
    }

    public static string Decrypt(string encrypted, byte[] key, string settingKey)
    {
        try
        {
            if (!encrypted.StartsWith(VersionPrefix, StringComparison.Ordinal))
            {
                throw new FormatException("Unsupported OAuth token envelope.");
            }

            var bytes = Convert.FromBase64String(encrypted[VersionPrefix.Length..]);
            var nonce = bytes[..NonceSize];
            var tag = bytes[^TagSize..];
            var ciphertext = bytes[NonceSize..^TagSize];
            var plaintext = new byte[ciphertext.Length];
            var aad = Encoding.UTF8.GetBytes(settingKey);

            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            throw new CredentialDecryptionException("OAuth credential could not be decrypted.", exception);
        }
    }
}

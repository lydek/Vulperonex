using System.Security.Cryptography;

namespace Vulperonex.Domain;

internal static class UlidGenerator
{
    private const string CrockfordBase32 = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string NewUlidString()
    {
        Span<byte> bytes = stackalloc byte[16];
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        bytes[0] = (byte)(timestamp >> 40);
        bytes[1] = (byte)(timestamp >> 32);
        bytes[2] = (byte)(timestamp >> 24);
        bytes[3] = (byte)(timestamp >> 16);
        bytes[4] = (byte)(timestamp >> 8);
        bytes[5] = (byte)timestamp;
        RandomNumberGenerator.Fill(bytes[6..]);

        return EncodeUlid(bytes);
    }

    private static string EncodeUlid(ReadOnlySpan<byte> bytes)
    {
        Span<char> chars = stackalloc char[26];

        chars[0] = CrockfordBase32[(bytes[0] & 0b1110_0000) >> 5];
        chars[1] = CrockfordBase32[bytes[0] & 0b0001_1111];
        chars[2] = CrockfordBase32[(bytes[1] & 0b1111_1000) >> 3];
        chars[3] = CrockfordBase32[((bytes[1] & 0b0000_0111) << 2) | ((bytes[2] & 0b1100_0000) >> 6)];
        chars[4] = CrockfordBase32[(bytes[2] & 0b0011_1110) >> 1];
        chars[5] = CrockfordBase32[((bytes[2] & 0b0000_0001) << 4) | ((bytes[3] & 0b1111_0000) >> 4)];
        chars[6] = CrockfordBase32[((bytes[3] & 0b0000_1111) << 1) | ((bytes[4] & 0b1000_0000) >> 7)];
        chars[7] = CrockfordBase32[(bytes[4] & 0b0111_1100) >> 2];
        chars[8] = CrockfordBase32[((bytes[4] & 0b0000_0011) << 3) | ((bytes[5] & 0b1110_0000) >> 5)];
        chars[9] = CrockfordBase32[bytes[5] & 0b0001_1111];
        chars[10] = CrockfordBase32[(bytes[6] & 0b1111_1000) >> 3];
        chars[11] = CrockfordBase32[((bytes[6] & 0b0000_0111) << 2) | ((bytes[7] & 0b1100_0000) >> 6)];
        chars[12] = CrockfordBase32[(bytes[7] & 0b0011_1110) >> 1];
        chars[13] = CrockfordBase32[((bytes[7] & 0b0000_0001) << 4) | ((bytes[8] & 0b1111_0000) >> 4)];
        chars[14] = CrockfordBase32[((bytes[8] & 0b0000_1111) << 1) | ((bytes[9] & 0b1000_0000) >> 7)];
        chars[15] = CrockfordBase32[(bytes[9] & 0b0111_1100) >> 2];
        chars[16] = CrockfordBase32[((bytes[9] & 0b0000_0011) << 3) | ((bytes[10] & 0b1110_0000) >> 5)];
        chars[17] = CrockfordBase32[bytes[10] & 0b0001_1111];
        chars[18] = CrockfordBase32[(bytes[11] & 0b1111_1000) >> 3];
        chars[19] = CrockfordBase32[((bytes[11] & 0b0000_0111) << 2) | ((bytes[12] & 0b1100_0000) >> 6)];
        chars[20] = CrockfordBase32[(bytes[12] & 0b0011_1110) >> 1];
        chars[21] = CrockfordBase32[((bytes[12] & 0b0000_0001) << 4) | ((bytes[13] & 0b1111_0000) >> 4)];
        chars[22] = CrockfordBase32[((bytes[13] & 0b0000_1111) << 1) | ((bytes[14] & 0b1000_0000) >> 7)];
        chars[23] = CrockfordBase32[(bytes[14] & 0b0111_1100) >> 2];
        chars[24] = CrockfordBase32[((bytes[14] & 0b0000_0011) << 3) | ((bytes[15] & 0b1110_0000) >> 5)];
        chars[25] = CrockfordBase32[bytes[15] & 0b0001_1111];

        return new string(chars);
    }
}

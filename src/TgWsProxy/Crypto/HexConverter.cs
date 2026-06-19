using System.Security.Cryptography;

namespace TgWsProxy.Crypto;

/// <summary>
/// Hexadecimal helper methods.
/// </summary>
public static class HexConverter
{
    /// <summary>
    /// Converts a hexadecimal string to bytes.
    /// </summary>
    public static byte[] FromHex(string value)
    {
        return Convert.FromHexString(value.Trim());
    }

    /// <summary>
    /// Generates a 16-byte secret encoded as 32 lowercase hexadecimal characters.
    /// </summary>
    public static string GenerateSecret()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }
}

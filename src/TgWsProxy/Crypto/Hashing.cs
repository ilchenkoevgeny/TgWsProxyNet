using System.Security.Cryptography;

namespace TgWsProxy.Crypto;

/// <summary>
/// Cryptographic hash helpers.
/// </summary>
public static class Hashing
{
    /// <summary>
    /// Calculates SHA-256 over two concatenated byte spans.
    /// </summary>
    public static byte[] Sha256(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var buffer = new byte[first.Length + second.Length];
        first.CopyTo(buffer);
        second.CopyTo(buffer.AsSpan(first.Length));
        return SHA256.HashData(buffer);
    }
}

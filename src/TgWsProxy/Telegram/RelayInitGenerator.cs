using System.Buffers.Binary;
using System.Security.Cryptography;
using TgWsProxy.Constants;
using TgWsProxy.Crypto;

namespace TgWsProxy.Telegram;

/// <summary>
/// Generates a new obfuscated MTProto initialization packet for Telegram DC side.
/// </summary>
public static class RelayInitGenerator
{
    /// <summary>
    /// Creates a 64-byte encrypted initialization packet for the selected DC and MTProto transport protocol.
    /// </summary>
    public static byte[] Generate(ReadOnlySpan<byte> protoTag, short dcIndex)
    {
        while (true)
        {
            var random = RandomNumberGenerator.GetBytes(MtProtoConstants.HandshakeLength);

            if (IsReserved(random))
            {
                continue;
            }

            protoTag.CopyTo(random.AsSpan(MtProtoConstants.ProtoTagPosition, 4));
            BinaryPrimitives.WriteInt16LittleEndian(
                random.AsSpan(MtProtoConstants.DcIndexPosition, 2),
                dcIndex);

            var key = random.AsSpan(MtProtoConstants.SkipLength, MtProtoConstants.PreKeyLength);
            var iv = random.AsSpan(
                MtProtoConstants.SkipLength + MtProtoConstants.PreKeyLength,
                MtProtoConstants.IvLength);

            using var transform = new AesCtrTransform(key, iv);
            return transform.Update(random);
        }
    }

    private static bool IsReserved(ReadOnlySpan<byte> value)
    {
        if (value[0] == 0xEF)
        {
            return true;
        }

        foreach (var reservedStart in MtProtoConstants.ReservedStarts)
        {
            if (value[..4].SequenceEqual(reservedStart))
            {
                return true;
            }
        }

        return value.Slice(4, 4).SequenceEqual(MtProtoConstants.ReservedContinue);
    }
}

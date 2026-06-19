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
    /// Creates a 64-byte obfuscated initialization packet for the selected DC and MTProto transport protocol.
    /// </summary>
    public static byte[] Generate(ReadOnlySpan<byte> protoTag, short dcIndex)
    {
        if (protoTag.Length != 4)
        {
            throw new ArgumentException("Protocol tag must contain exactly 4 bytes.", nameof(protoTag));
        }

        while (true)
        {
            var random = RandomNumberGenerator.GetBytes(MtProtoConstants.HandshakeLength);

            if (IsReserved(random))
            {
                continue;
            }

            var key = random.AsSpan(MtProtoConstants.SkipLength, MtProtoConstants.PreKeyLength);
            var iv = random.AsSpan(
                MtProtoConstants.SkipLength + MtProtoConstants.PreKeyLength,
                MtProtoConstants.IvLength);

            byte[] encryptedFull;

            using (var transform = new AesCtrTransform(key, iv))
            {
                encryptedFull = transform.Update(random);
            }

            Span<byte> tailPlain = stackalloc byte[8];

            protoTag.CopyTo(tailPlain[..4]);

            BinaryPrimitives.WriteInt16LittleEndian(
                tailPlain.Slice(4, 2),
                dcIndex);

            RandomNumberGenerator.Fill(tailPlain.Slice(6, 2));

            var result = random.ToArray();

            for (var i = 0; i < 8; i++)
            {
                var position = MtProtoConstants.ProtoTagPosition + i;
                var keyStreamByte = (byte)(encryptedFull[position] ^ random[position]);

                result[position] = (byte)(tailPlain[i] ^ keyStreamByte);
            }

            return result;
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
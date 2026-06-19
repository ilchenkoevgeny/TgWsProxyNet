using System.Buffers.Binary;
using TgWsProxy.Constants;
using TgWsProxy.Crypto;

namespace TgWsProxy.Telegram;

/// <summary>
/// Parses the first 64-byte MTProto obfuscated handshake sent by Telegram Desktop.
/// </summary>
public static class MtProtoHandshakeParser
{
    /// <summary>
    /// Tries to parse and validate the client handshake using the configured proxy secret.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> handshake, ReadOnlySpan<byte> secret, out HandshakeResult? result)
    {
        result = null;

        if (handshake.Length != MtProtoConstants.HandshakeLength)
        {
            return false;
        }

        var clientDecryptPreKeyIv = handshake.Slice(
            MtProtoConstants.SkipLength,
            MtProtoConstants.PreKeyLength + MtProtoConstants.IvLength).ToArray();

        var preKey = clientDecryptPreKeyIv.AsSpan(0, MtProtoConstants.PreKeyLength);
        var iv = clientDecryptPreKeyIv.AsSpan(MtProtoConstants.PreKeyLength, MtProtoConstants.IvLength);
        var key = Hashing.Sha256(preKey, secret);

        using var transform = new AesCtrTransform(key, iv);
        var decrypted = transform.Update(handshake);
        var protoTag = decrypted.AsSpan(MtProtoConstants.ProtoTagPosition, 4);

        uint protoInt;

        if (protoTag.SequenceEqual(MtProtoConstants.ProtoTagAbridged))
        {
            protoInt = MtProtoConstants.ProtoAbridged;
        }
        else if (protoTag.SequenceEqual(MtProtoConstants.ProtoTagIntermediate))
        {
            protoInt = MtProtoConstants.ProtoIntermediate;
        }
        else if (protoTag.SequenceEqual(MtProtoConstants.ProtoTagSecure))
        {
            protoInt = MtProtoConstants.ProtoPaddedIntermediate;
        }
        else
        {
            return false;
        }

        var dcIndex = BinaryPrimitives.ReadInt16LittleEndian(
            decrypted.AsSpan(MtProtoConstants.DcIndexPosition, 2));

        result = new HandshakeResult(
            Math.Abs(dcIndex),
            dcIndex < 0,
            protoTag.ToArray(),
            protoInt,
            clientDecryptPreKeyIv);

        return true;
    }
}

using System.Buffers.Binary;
using TgWsProxy.Constants;
using TgWsProxy.Crypto;

namespace TgWsProxy.Proxy;

/// <summary>
/// Splits Telegram-bound encrypted bytes into MTProto transport packets before sending them as WebSocket frames.
/// </summary>
public sealed class MessageSplitter : IDisposable
{
    private readonly uint protocol;
    private readonly AesCtrTransform decryptor;
    private readonly List<byte> cipherBuffer = [];
    private readonly List<byte> plainBuffer = [];
    private bool disabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageSplitter"/> class.
    /// </summary>
    public MessageSplitter(ReadOnlySpan<byte> relayInit, uint protocol)
    {
        this.protocol = protocol;

        var key = relayInit.Slice(MtProtoConstants.SkipLength, MtProtoConstants.PreKeyLength);
        var iv = relayInit.Slice(
            MtProtoConstants.SkipLength + MtProtoConstants.PreKeyLength,
            MtProtoConstants.IvLength);

        decryptor = new AesCtrTransform(key, iv);
        decryptor.Update(MtProtoConstants.Zero64);
    }

    /// <summary>
    /// Adds encrypted bytes and returns complete encrypted packet slices.
    /// </summary>
    public IReadOnlyList<byte[]> Split(ReadOnlySpan<byte> chunk)
    {
        if (chunk.IsEmpty)
        {
            return [];
        }

        if (disabled)
        {
            return [chunk.ToArray()];
        }

        cipherBuffer.AddRange(chunk.ToArray());
        plainBuffer.AddRange(decryptor.Update(chunk));

        var result = new List<byte[]>();
        var offset = 0;

        while (offset < cipherBuffer.Count)
        {
            var packetLength = GetNextPacketLength(offset, cipherBuffer.Count - offset);

            if (packetLength is null)
            {
                break;
            }

            if (packetLength <= 0)
            {
                result.Add(cipherBuffer.Skip(offset).ToArray());
                offset = cipherBuffer.Count;
                disabled = true;
                break;
            }

            result.Add(cipherBuffer.Skip(offset).Take(packetLength.Value).ToArray());
            offset += packetLength.Value;
        }

        if (offset > 0)
        {
            cipherBuffer.RemoveRange(0, offset);
            plainBuffer.RemoveRange(0, offset);
        }

        return result;
    }

    /// <summary>
    /// Returns any buffered encrypted tail.
    /// </summary>
    public IReadOnlyList<byte[]> Flush()
    {
        if (cipherBuffer.Count == 0)
        {
            return [];
        }

        var tail = cipherBuffer.ToArray();
        cipherBuffer.Clear();
        plainBuffer.Clear();
        return [tail];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        decryptor.Dispose();
    }

    private int? GetNextPacketLength(int offset, int available)
    {
        if (available <= 0)
        {
            return null;
        }

        return protocol switch
        {
            MtProtoConstants.ProtoAbridged => GetAbridgedPacketLength(offset, available),
            MtProtoConstants.ProtoIntermediate or MtProtoConstants.ProtoPaddedIntermediate => GetIntermediatePacketLength(offset, available),
            _ => 0
        };
    }

    private int? GetAbridgedPacketLength(int offset, int available)
    {
        var first = plainBuffer[offset];
        int payloadLength;
        int headerLength;

        if (first is 0x7F or 0xFF)
        {
            if (available < 4)
            {
                return null;
            }

            payloadLength = (plainBuffer[offset + 1]
                | (plainBuffer[offset + 2] << 8)
                | (plainBuffer[offset + 3] << 16)) * 4;
            headerLength = 4;
        }
        else
        {
            payloadLength = (first & 0x7F) * 4;
            headerLength = 1;
        }

        if (payloadLength <= 0)
        {
            return 0;
        }

        var packetLength = headerLength + payloadLength;
        return available < packetLength ? null : packetLength;
    }

    private int? GetIntermediatePacketLength(int offset, int available)
    {
        if (available < 4)
        {
            return null;
        }

        Span<byte> lengthBytes = stackalloc byte[4];
        lengthBytes[0] = plainBuffer[offset];
        lengthBytes[1] = plainBuffer[offset + 1];
        lengthBytes[2] = plainBuffer[offset + 2];
        lengthBytes[3] = plainBuffer[offset + 3];

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes) & 0x7FFFFFFF;

        if (payloadLength <= 0)
        {
            return 0;
        }

        var packetLength = 4 + payloadLength;
        return available < packetLength ? null : packetLength;
    }
}

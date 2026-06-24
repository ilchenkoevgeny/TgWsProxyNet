using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;

namespace TgWsProxy.WebSockets;

/// <summary>
/// Minimal raw WebSocket client used to talk to Telegram /apiws endpoints through a chosen target IP and Host/SNI domain.
/// </summary>
public sealed class RawWebSocketClient : IAsyncDisposable
{
    private const byte OpContinuation = 0x0;
    private const byte OpBinary = 0x2;
    private const byte OpClose = 0x8;
    private const byte OpPing = 0x9;
    private const byte OpPong = 0xA;

    private readonly TcpClient tcpClient;
    private readonly SslStream sslStream;
    private readonly SemaphoreSlim sendLock = new(1, 1);

    private bool closed;

    private RawWebSocketClient(TcpClient tcpClient, SslStream sslStream)
    {
        this.tcpClient = tcpClient;
        this.sslStream = sslStream;
    }

    /// <summary>
    /// Connects to the Telegram WebSocket endpoint.
    /// </summary>
    public static async Task<RawWebSocketClient> ConnectAsync(
        string host,
        string domain,
        bool skipTlsCertificateValidation,
        TimeSpan timeout,
        string path,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);

        var tcpClient = new TcpClient
        {
            NoDelay = true,
            ReceiveBufferSize = 1024 * 1024,
            SendBufferSize = 1024 * 1024
        };

        await tcpClient.ConnectAsync(host, 443, linkedCts.Token);

        var sslStream = new SslStream(
            tcpClient.GetStream(),
            leaveInnerStreamOpen: false,
            userCertificateValidationCallback: skipTlsCertificateValidation
                ? (_, _, _, _) => true
                : null);

        await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = domain,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
        }, linkedCts.Token);

        var wsKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var request =
            $"GET {path} HTTP/1.1\r\n" +
            $"Host: {domain}\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Key: {wsKey}\r\n" +
            "Sec-WebSocket-Version: 13\r\n" +
            "Sec-WebSocket-Protocol: binary\r\n" +
            "\r\n";

        await sslStream.WriteAsync(Encoding.ASCII.GetBytes(request), linkedCts.Token);
        await sslStream.FlushAsync(linkedCts.Token);

        var response = await ReadHttpHeadersAsync(sslStream, linkedCts.Token);
        var firstLine = response.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var parts = firstLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

        var statusCode = 0;

        if (parts.Length >= 2)
        {
            _ = int.TryParse(parts[1], out statusCode);
        }

        if (statusCode == 101)
        {
            return new RawWebSocketClient(tcpClient, sslStream);
        }

        await sslStream.DisposeAsync();
        tcpClient.Dispose();
        throw new WebSocketHandshakeException(statusCode, firstLine);
    }

    /// <summary>
    /// Sends a binary WebSocket frame.
    /// </summary>
    public Task SendBinaryAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        return SendFrameAsync(OpBinary, data, mask: true, cancellationToken);
    }

    /// <summary>
    /// Sends a ping frame.
    /// </summary>
    public Task SendPingAsync(ReadOnlyMemory<byte> payload = default, CancellationToken cancellationToken = default)
    {
        return SendFrameAsync(OpPing, payload, mask: true, cancellationToken);
    }

    /// <summary>
    /// Receives the next binary payload. Returns null if the peer closed the socket.
    /// </summary>
    public async Task<byte[]?> ReceiveBinaryAsync(CancellationToken cancellationToken)
    {
        while (!closed)
        {
            var frame = await ReadFrameAsync(cancellationToken);

            switch (frame.Opcode)
            {
                case OpClose:
                    closed = true;

                    try
                    {
                        await SendFrameAsync(OpClose, frame.Payload, mask: true, cancellationToken);
                    }
                    catch
                    {
                        // Ignore close response errors.
                    }

                    return null;

                case OpPing:
                    await SendFrameAsync(OpPong, frame.Payload, mask: true, cancellationToken);
                    continue;

                case OpPong:
                    continue;

                case OpBinary:
                case OpContinuation:
                    return frame.Payload;

                default:
                    continue;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!closed)
        {
            closed = true;

            try
            {
                await SendFrameAsync(OpClose, ReadOnlyMemory<byte>.Empty, mask: true, CancellationToken.None);
            }
            catch
            {
                // Ignore close errors.
            }
        }

        sendLock.Dispose();
        await sslStream.DisposeAsync();
        tcpClient.Dispose();
    }

    private async Task SendFrameAsync(
        byte opcode,
        ReadOnlyMemory<byte> data,
        bool mask,
        CancellationToken cancellationToken)
    {
        await sendLock.WaitAsync(cancellationToken);

        try
        {
            if (closed && opcode != OpClose)
            {
                throw new IOException("WebSocket is closed.");
            }

            var frame = BuildFrame(opcode, data.Span, mask);

            await sslStream.WriteAsync(frame, cancellationToken);
            await sslStream.FlushAsync(cancellationToken);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private static byte[] BuildFrame(byte opcode, ReadOnlySpan<byte> payload, bool mask)
    {
        var length = payload.Length;
        var headerLength = length < 126 ? 2 : length <= ushort.MaxValue ? 4 : 10;
        var maskLength = mask ? 4 : 0;
        var frame = new byte[headerLength + maskLength + length];

        frame[0] = (byte)(0x80 | opcode);

        if (length < 126)
        {
            frame[1] = (byte)(mask ? 0x80 | length : length);
        }
        else if (length <= ushort.MaxValue)
        {
            frame[1] = (byte)(mask ? 0x80 | 126 : 126);
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2, 2), (ushort)length);
        }
        else
        {
            frame[1] = (byte)(mask ? 0x80 | 127 : 127);
            BinaryPrimitives.WriteUInt64BigEndian(frame.AsSpan(2, 8), (ulong)length);
        }

        var payloadOffset = headerLength;

        if (mask)
        {
            var maskKey = RandomNumberGenerator.GetBytes(4);
            maskKey.CopyTo(frame.AsSpan(headerLength, 4));
            payloadOffset += 4;

            for (var i = 0; i < payload.Length; i++)
            {
                frame[payloadOffset + i] = (byte)(payload[i] ^ maskKey[i % 4]);
            }
        }
        else
        {
            payload.CopyTo(frame.AsSpan(payloadOffset));
        }

        return frame;
    }

    private async Task<WebSocketFrame> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var header = await ReadExactAsync(2, cancellationToken);
        var opcode = (byte)(header[0] & 0x0F);
        var masked = (header[1] & 0x80) != 0;
        ulong length = (ulong)(header[1] & 0x7F);

        if (length == 126)
        {
            length = BinaryPrimitives.ReadUInt16BigEndian(await ReadExactAsync(2, cancellationToken));
        }
        else if (length == 127)
        {
            length = BinaryPrimitives.ReadUInt64BigEndian(await ReadExactAsync(8, cancellationToken));
        }

        if (length > int.MaxValue)
        {
            throw new IOException("WebSocket frame is too large.");
        }

        byte[]? maskKey = null;

        if (masked)
        {
            maskKey = await ReadExactAsync(4, cancellationToken);
        }

        var payload = await ReadExactAsync((int)length, cancellationToken);

        if (maskKey is not null)
        {
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] ^= maskKey[i % 4];
            }
        }

        return new WebSocketFrame(opcode, payload);
    }

    private async Task<byte[]> ReadExactAsync(int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var read = await sslStream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);

            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }

        return buffer;
    }

    private static async Task<string> ReadHttpHeadersAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new List<byte>(4096);
        var one = new byte[1];

        while (buffer.Count < 32768)
        {
            var read = await stream.ReadAsync(one, cancellationToken);

            if (read == 0)
            {
                break;
            }

            buffer.Add(one[0]);

            if (buffer.Count >= 4 &&
                buffer[^4] == '\r' &&
                buffer[^3] == '\n' &&
                buffer[^2] == '\r' &&
                buffer[^1] == '\n')
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private sealed record WebSocketFrame(byte Opcode, byte[] Payload);
}
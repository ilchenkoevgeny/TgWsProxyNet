using System.Net.Sockets;
using System.Security.Authentication;
using TgWsProxy.Config;
using TgWsProxy.Constants;
using TgWsProxy.Crypto;
using TgWsProxy.Telegram;
using TgWsProxy.WebSockets;

namespace TgWsProxy.Proxy;

/// <summary>
/// Handles one Telegram Desktop TCP connection.
/// </summary>
public sealed class ClientSession : IAsyncDisposable
{
    private readonly TcpClient client;
    private readonly ProxyOptions options;
    private readonly ILogger<ClientSession> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientSession"/> class.
    /// </summary>
    public ClientSession(
        TcpClient client,
        ProxyOptions options,
        ILogger<ClientSession> logger)
    {
        this.client = client;
        this.options = options;
        this.logger = logger;
    }

    /// <summary>
    /// Runs the session until client/upstream disconnects.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var label = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

        try
        {
            await using var clientStream = client.GetStream();
            var secret = HexConverter.FromHex(options.Secret);
            var handshake = await ReadExactAsync(clientStream, MtProtoConstants.HandshakeLength, cancellationToken);

            if (!MtProtoHandshakeParser.TryParse(handshake, secret, out var parsed) || parsed is null)
            {
                logger.LogWarning("[{Label}] bad handshake: wrong secret or unsupported protocol", label);
                return;
            }

            var dcIndex = (short)(parsed.IsMedia ? -parsed.DcId : parsed.DcId);
            var relayInit = RelayInitGenerator.Generate(parsed.ProtoTag, dcIndex);

            using var cryptoContext = CryptoContextFactory.Create(
                parsed.ClientDecryptPreKeyIv,
                secret,
                relayInit);

            logger.LogInformation(
                "[{Label}] handshake ok: DC{DcId}{MediaSuffix}, proto=0x{Protocol:X8}",
                label,
                parsed.DcId,
                parsed.IsMedia ? " media" : string.Empty,
                parsed.ProtoInt);

            if (await TryBridgeViaWebSocketAsync(
                clientStream,
                parsed,
                relayInit,
                cryptoContext,
                label,
                cancellationToken))
            {
                return;
            }

            if (options.EnableCloudflareProxyFallback)
            {
                if (await TryBridgeViaCloudflareProxyAsync(
                    clientStream,
                    parsed,
                    relayInit,
                    cryptoContext,
                    label,
                    cancellationToken))
                {
                    return;
                }
            }

            if (options.EnableDirectTcpFallback)
            {
                await BridgeViaDirectTcpAsync(
                    clientStream,
                    parsed,
                    relayInit,
                    cryptoContext,
                    label,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (EndOfStreamException)
        {
            logger.LogDebug("[{Label}] client disconnected", label);
        }
        catch (SocketException ex)
        {
            logger.LogDebug(ex, "[{Label}] socket error", label);
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "[{Label}] IO error", label);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Label}] unexpected session error", label);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await Task.CompletedTask;
    }

    private async Task<bool> TryBridgeViaWebSocketAsync(
        Stream clientStream,
        HandshakeResult parsed,
        byte[] relayInit,
        CryptoContext cryptoContext,
        string label,
        CancellationToken cancellationToken)
    {
        if (parsed.DcId >= 200)
        {
            logger.LogInformation(
                "[{Label}] DC{DcId} is CDN/media DC -> Cloudflare proxy fallback",
                label,
                parsed.DcId);

            return false;
        }

        if (!options.DcEndpoints.TryGetValue(parsed.DcId, out var targetIp))
        {
            logger.LogInformation("[{Label}] DC{DcId} has no WebSocket target in config", label, parsed.DcId);
            return false;
        }

        foreach (var domain in DcResolver.GetWebSocketDomains(parsed.DcId, parsed.IsMedia))
        {
            try
            {
                logger.LogInformation(
                    "[{Label}] DC{DcId}{MediaSuffix} -> wss://{Domain}/apiws via {TargetIp}",
                    label,
                    parsed.DcId,
                    parsed.IsMedia ? " media" : string.Empty,
                    domain,
                    targetIp);

                await using var webSocket = await RawWebSocketClient.ConnectAsync(
                    targetIp,
                    domain,
                    options.SkipTlsCertificateValidation,
                    TimeSpan.FromSeconds(options.WebSocketConnectTimeoutSeconds),
                    "/apiws",
                    cancellationToken);

                await webSocket.SendBinaryAsync(relayInit, cancellationToken);

                using var splitter = new MessageSplitter(relayInit, parsed.ProtoInt);

                await TrafficBridge.BridgeWebSocketAsync(
                    clientStream,
                    webSocket,
                    cryptoContext,
                    splitter,
                    TimeSpan.FromSeconds(options.WebSocketKeepAliveSeconds),
                    logger,
                    label,
                    cancellationToken);

                return true;
            }
            catch (WebSocketHandshakeException ex)
            {
                logger.LogWarning(
                    "[{Label}] WebSocket handshake failed for {Domain}: {StatusLine}",
                    label,
                    domain,
                    ex.StatusLine);
            }
            catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException or TimeoutException)
            {
                logger.LogWarning(ex, "[{Label}] WebSocket connect failed for {Domain}", label, domain);
            }
        }

        return false;
    }

    private async Task<bool> TryBridgeViaCloudflareProxyAsync(
        Stream clientStream,
        HandshakeResult parsed,
        byte[] relayInit,
        CryptoContext cryptoContext,
        string label,
        CancellationToken cancellationToken)
    {
        var baseDomains = options.CloudflareProxyDomains
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static _ => Random.Shared.Next())
            .ToArray();

        if (baseDomains.Length == 0)
        {
            logger.LogInformation("[{Label}] Cloudflare proxy fallback is enabled, but no domains are configured", label);
            return false;
        }

        logger.LogInformation(
            "[{Label}] DC{DcId}{MediaSuffix} -> trying Cloudflare proxy",
            label,
            parsed.DcId,
            parsed.IsMedia ? " media" : string.Empty);

        foreach (var baseDomain in baseDomains)
        {
            var domain = $"kws{parsed.DcId}.{baseDomain}";

            try
            {
                logger.LogInformation(
                    "[{Label}] DC{DcId}{MediaSuffix} -> wss://{Domain}/apiws",
                    label,
                    parsed.DcId,
                    parsed.IsMedia ? " media" : string.Empty,
                    domain);

                await using var webSocket = await RawWebSocketClient.ConnectAsync(
                    domain,
                    domain,
                    options.SkipTlsCertificateValidation,
                    TimeSpan.FromSeconds(options.WebSocketConnectTimeoutSeconds),
                    "/apiws",
                    cancellationToken);

                await webSocket.SendBinaryAsync(relayInit, cancellationToken);

                using var splitter = new MessageSplitter(relayInit, parsed.ProtoInt);

                await TrafficBridge.BridgeWebSocketAsync(
                    clientStream,
                    webSocket,
                    cryptoContext,
                    splitter,
                    TimeSpan.FromSeconds(options.WebSocketKeepAliveSeconds),
                    logger,
                    label,
                    cancellationToken);

                return true;
            }
            catch (WebSocketHandshakeException ex)
            {
                logger.LogWarning(
                    "[{Label}] DC{DcId}{MediaSuffix} Cloudflare proxy handshake failed for {Domain}: {StatusLine}",
                    label,
                    parsed.DcId,
                    parsed.IsMedia ? " media" : string.Empty,
                    domain,
                    ex.StatusLine);
            }
            catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException or TimeoutException)
            {
                logger.LogWarning(
                    ex,
                    "[{Label}] DC{DcId}{MediaSuffix} Cloudflare proxy failed for {Domain}",
                    label,
                    parsed.DcId,
                    parsed.IsMedia ? " media" : string.Empty,
                    domain);
            }
        }

        return false;
    }

    private async Task BridgeViaDirectTcpAsync(
        Stream clientStream,
        HandshakeResult parsed,
        byte[] relayInit,
        CryptoContext cryptoContext,
        string label,
        CancellationToken cancellationToken)
    {
        if (!options.DirectTcpFallbackEndpoints.TryGetValue(parsed.DcId, out var fallbackIp))
        {
            logger.LogWarning("[{Label}] no direct TCP fallback IP for DC{DcId}", label, parsed.DcId);
            return;
        }

        logger.LogInformation(
            "[{Label}] DC{DcId}{MediaSuffix} -> direct TCP fallback {FallbackIp}:443",
            label,
            parsed.DcId,
            parsed.IsMedia ? " media" : string.Empty,
            fallbackIp);

        using var upstream = new TcpClient
        {
            NoDelay = true,
            ReceiveBufferSize = options.GetBufferSizeBytes(),
            SendBufferSize = options.GetBufferSizeBytes()
        };

        await upstream.ConnectAsync(fallbackIp, 443, cancellationToken);
        await using var upstreamStream = upstream.GetStream();

        await upstreamStream.WriteAsync(relayInit, cancellationToken);
        await upstreamStream.FlushAsync(cancellationToken);

        await TrafficBridge.BridgeTcpAsync(
            clientStream,
            upstreamStream,
            cryptoContext,
            logger,
            label,
            cancellationToken);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);

            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }

        return buffer;
    }
}

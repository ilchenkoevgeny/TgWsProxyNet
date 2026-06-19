using TgWsProxy.Crypto;
using TgWsProxy.WebSockets;

namespace TgWsProxy.Proxy;

/// <summary>
/// Bidirectional relay between Telegram Desktop TCP stream and Telegram DC upstream.
/// </summary>
public static class TrafficBridge
{
    /// <summary>
    /// Bridges local TCP client with Telegram WebSocket upstream and re-encrypts traffic in both directions.
    /// </summary>
    public static async Task BridgeWebSocketAsync(
        Stream clientStream,
        RawWebSocketClient webSocket,
        CryptoContext cryptoContext,
        MessageSplitter? splitter,
        TimeSpan keepAliveInterval,
        ILogger logger,
        string label,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linkedCts.Token;

        var tcpToWsTask = Task.Run(
            () => TcpToWebSocketAsync(clientStream, webSocket, cryptoContext, splitter, logger, label, token),
            token);
        var wsToTcpTask = Task.Run(
            () => WebSocketToTcpAsync(clientStream, webSocket, cryptoContext, logger, label, token),
            token);
        var keepAliveTask = Task.Run(
            () => KeepAliveAsync(webSocket, keepAliveInterval, token),
            token);

        await Task.WhenAny(tcpToWsTask, wsToTcpTask);
        await linkedCts.CancelAsync();

        await SafeAwait(tcpToWsTask);
        await SafeAwait(wsToTcpTask);
        await SafeAwait(keepAliveTask);
    }

    /// <summary>
    /// Bridges local TCP client with Telegram direct TCP upstream and re-encrypts traffic in both directions.
    /// </summary>
    public static async Task BridgeTcpAsync(
        Stream clientStream,
        Stream telegramStream,
        CryptoContext cryptoContext,
        ILogger logger,
        string label,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linkedCts.Token;

        var upTask = Task.Run(
            () => ForwardTcpAsync(clientStream, telegramStream, cryptoContext.ClientDecryptor, cryptoContext.TelegramEncryptor, token),
            token);
        var downTask = Task.Run(
            () => ForwardTcpAsync(telegramStream, clientStream, cryptoContext.TelegramDecryptor, cryptoContext.ClientEncryptor, token),
            token);

        await Task.WhenAny(upTask, downTask);
        await linkedCts.CancelAsync();

        await SafeAwait(upTask);
        await SafeAwait(downTask);
        logger.LogInformation("[{Label}] TCP session closed", label);
    }

    private static async Task TcpToWebSocketAsync(
        Stream clientStream,
        RawWebSocketClient webSocket,
        CryptoContext cryptoContext,
        MessageSplitter? splitter,
        ILogger logger,
        string label,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await clientStream.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                if (splitter is not null)
                {
                    foreach (var tail in splitter.Flush())
                    {
                        await webSocket.SendBinaryAsync(tail, cancellationToken);
                    }
                }

                return;
            }

            var plain = cryptoContext.ClientDecryptor.Update(buffer.AsSpan(0, read));
            var telegramCipher = cryptoContext.TelegramEncryptor.Update(plain);

            if (splitter is null)
            {
                await webSocket.SendBinaryAsync(telegramCipher, cancellationToken);
                continue;
            }

            foreach (var part in splitter.Split(telegramCipher))
            {
                await webSocket.SendBinaryAsync(part, cancellationToken);
            }
        }

        logger.LogDebug("[{Label}] tcp->ws finished", label);
    }

    private static async Task WebSocketToTcpAsync(
        Stream clientStream,
        RawWebSocketClient webSocket,
        CryptoContext cryptoContext,
        ILogger logger,
        string label,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var data = await webSocket.ReceiveBinaryAsync(cancellationToken);

            if (data is null)
            {
                return;
            }

            var plain = cryptoContext.TelegramDecryptor.Update(data);
            var clientCipher = cryptoContext.ClientEncryptor.Update(plain);
            await clientStream.WriteAsync(clientCipher, cancellationToken);
            await clientStream.FlushAsync(cancellationToken);
        }

        logger.LogDebug("[{Label}] ws->tcp finished", label);
    }

    private static async Task ForwardTcpAsync(
        Stream source,
        Stream destination,
        AesCtrTransform decryptor,
        AesCtrTransform encryptor,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                return;
            }

            var plain = decryptor.Update(buffer.AsSpan(0, read));
            var cipher = encryptor.Update(plain);
            await destination.WriteAsync(cipher, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }
    }

    private static async Task KeepAliveAsync(
        RawWebSocketClient webSocket,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        if (interval <= TimeSpan.Zero)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken);
            await webSocket.SendPingAsync(cancellationToken: cancellationToken);
        }
    }

    private static async Task SafeAwait(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected on session shutdown.
        }
        catch
        {
            // Session side already failed. The owner logs the session lifecycle.
        }
    }
}

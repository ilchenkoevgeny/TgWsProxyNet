using System.Net.Sockets;
using Microsoft.Extensions.Options;
using TgWsProxy.Config;

namespace TgWsProxy.Proxy;

/// <summary>
/// Local TCP server that accepts Telegram Desktop MTProto proxy connections.
/// </summary>
public sealed class ProxyServer
{
    private readonly IOptions<ProxyOptions> options;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<ProxyServer> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProxyServer"/> class.
    /// </summary>
    public ProxyServer(
        IOptions<ProxyOptions> options,
        ILoggerFactory loggerFactory,
        ILogger<ProxyServer> logger)
    {
        this.options = options;
        this.loggerFactory = loggerFactory;
        this.logger = logger;
    }

    /// <summary>
    /// Starts the local listener and serves clients until cancellation is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(options.Value.GetLocalEndPoint());
        listener.Server.NoDelay = true;
        listener.Start();

        logger.LogInformation("Proxy server started on {Host}:{Port}", options.Value.Host, options.Value.Port);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var tcpClient = await listener.AcceptTcpClientAsync(cancellationToken);
                ConfigureClientSocket(tcpClient);

                _ = Task.Run(
                    () => HandleClientAsync(tcpClient, cancellationToken),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during service shutdown.
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        await using var session = new ClientSession(
            tcpClient,
            options.Value,
            loggerFactory.CreateLogger<ClientSession>());

        await session.RunAsync(cancellationToken);
    }

    private void ConfigureClientSocket(TcpClient tcpClient)
    {
        var bufferSize = options.Value.GetBufferSizeBytes();
        tcpClient.NoDelay = true;
        tcpClient.ReceiveBufferSize = bufferSize;
        tcpClient.SendBufferSize = bufferSize;
    }
}

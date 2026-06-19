using Microsoft.Extensions.Options;
using TgWsProxy.Config;
using TgWsProxy.Logging;
using TgWsProxy.Proxy;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "TgWsProxy";
});

builder.Services
    .AddOptions<ProxyOptions>()
    .Bind(builder.Configuration.GetSection(ProxyOptions.SectionName))
    .Validate(options => options.IsSecretValid(), "Proxy:Secret must contain exactly 32 hexadecimal characters.")
    .ValidateOnStart();

var proxyOptions = builder.Configuration.GetSection(ProxyOptions.SectionName).Get<ProxyOptions>() ?? new ProxyOptions();

builder.Logging.AddSimpleFile(proxyOptions.LogFile);

builder.Services.AddSingleton<ProxyServer>();
builder.Services.AddHostedService<TgWsProxyWorker>();

await builder.Build().RunAsync();

/// <summary>
/// Hosted service wrapper around the TCP proxy server.
/// </summary>
public sealed class TgWsProxyWorker : BackgroundService
{
    private readonly ProxyServer proxyServer;
    private readonly IOptions<ProxyOptions> options;
    private readonly ILogger<TgWsProxyWorker> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TgWsProxyWorker"/> class.
    /// </summary>
    public TgWsProxyWorker(
        ProxyServer proxyServer,
        IOptions<ProxyOptions> options,
        ILogger<TgWsProxyWorker> logger)
    {
        this.proxyServer = proxyServer;
        this.options = options;
        this.logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Telegram MTProto WS Bridge Proxy");
        logger.LogInformation("Listening on {Host}:{Port}", options.Value.Host, options.Value.Port);
        logger.LogInformation("Telegram link: {ProxyLink}", options.Value.GetTelegramProxyLink());

        await proxyServer.RunAsync(stoppingToken);
    }
}

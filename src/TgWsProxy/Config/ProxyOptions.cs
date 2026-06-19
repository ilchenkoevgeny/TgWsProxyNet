using System.Net;
using System.Text.RegularExpressions;

namespace TgWsProxy.Config;

/// <summary>
/// Runtime settings for the local Telegram proxy.
/// </summary>
public sealed partial class ProxyOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Proxy";

    /// <summary>
    /// Local address to bind. Keep it 127.0.0.1 for personal desktop usage.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Local TCP port used by Telegram Desktop.
    /// </summary>
    public int Port { get; set; } = 1443;

    /// <summary>
    /// Internal 16-byte MTProto proxy secret encoded as 32 hexadecimal characters.
    /// Telegram Desktop uses the same value with dd prefix.
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Optional log file path. Relative paths are resolved against the application directory.
    /// </summary>
    public string? LogFile { get; set; } = "logs/tg-ws-proxy.log";

    /// <summary>
    /// Socket receive/send buffer size in kilobytes.
    /// </summary>
    public int BufferSizeKb { get; set; } = 256;

    /// <summary>
    /// WebSocket connection timeout in seconds.
    /// </summary>
    public int WebSocketConnectTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// WebSocket ping interval in seconds. Set 0 to disable.
    /// </summary>
    public int WebSocketKeepAliveSeconds { get; set; } = 30;

    /// <summary>
    /// Allows TLS connections to Telegram WebSocket endpoints without certificate validation.
    /// The original Python project does this unconditionally; here it is configurable.
    /// </summary>
    public bool SkipTlsCertificateValidation { get; set; } = true;

    /// <summary>
    /// Enables direct TCP fallback to Telegram DC if WebSocket connection cannot be established.
    /// </summary>
    public bool EnableDirectTcpFallback { get; set; } = true;

    /// <summary>
    /// WebSocket target IPs by Telegram DC ID.
    /// </summary>
    public Dictionary<int, string> DcEndpoints { get; set; } = new()
    {
        [2] = "149.154.167.220",
        [4] = "149.154.167.220"
    };

    /// <summary>
    /// Default direct TCP fallback IPs by Telegram DC ID.
    /// </summary>
    public Dictionary<int, string> DirectTcpFallbackEndpoints { get; set; } = new()
    {
        [1] = "149.154.175.50",
        [2] = "149.154.167.51",
        [3] = "149.154.175.100",
        [4] = "149.154.167.91",
        [5] = "149.154.171.5",
        [203] = "91.105.192.100"
    };

    /// <summary>
    /// Gets local bind endpoint.
    /// </summary>
    public IPEndPoint GetLocalEndPoint()
    {
        return new IPEndPoint(IPAddress.Parse(Host), Port);
    }

    /// <summary>
    /// Gets socket buffer size in bytes.
    /// </summary>
    public int GetBufferSizeBytes()
    {
        return Math.Max(4, BufferSizeKb) * 1024;
    }

    /// <summary>
    /// Returns Telegram Desktop MTProto proxy link.
    /// </summary>
    public string GetTelegramProxyLink()
    {
        return $"tg://proxy?server={Host}&port={Port}&secret=dd{Secret}";
    }

    /// <summary>
    /// Validates secret format.
    /// </summary>
    public bool IsSecretValid()
    {
        return !string.IsNullOrWhiteSpace(Secret) && SecretRegex().IsMatch(Secret);
    }

    [GeneratedRegex("^[0-9a-fA-F]{32}$", RegexOptions.Compiled)]
    private static partial Regex SecretRegex();
}

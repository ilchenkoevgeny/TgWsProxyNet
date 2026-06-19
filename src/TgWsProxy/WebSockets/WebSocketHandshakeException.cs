namespace TgWsProxy.WebSockets;

/// <summary>
/// Represents failed HTTP upgrade to WebSocket.
/// </summary>
public sealed class WebSocketHandshakeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketHandshakeException"/> class.
    /// </summary>
    public WebSocketHandshakeException(int statusCode, string statusLine)
        : base($"WebSocket handshake failed: HTTP {statusCode}; {statusLine}")
    {
        StatusCode = statusCode;
        StatusLine = statusLine;
    }

    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// HTTP status line.
    /// </summary>
    public string StatusLine { get; }
}

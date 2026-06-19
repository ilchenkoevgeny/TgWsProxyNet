namespace TgWsProxy.Telegram;

/// <summary>
/// Resolves Telegram WebSocket domains for a DC.
/// </summary>
public static class DcResolver
{
    /// <summary>
    /// Returns candidate Telegram WebSocket domains for the specified DC.
    /// </summary>
    public static IReadOnlyList<string> GetWebSocketDomains(int dcId, bool isMedia)
    {
        if (dcId == 203)
        {
            dcId = 2;
        }

        return isMedia
            ? [$"kws{dcId}-1.web.telegram.org", $"kws{dcId}.web.telegram.org"]
            : [$"kws{dcId}.web.telegram.org", $"kws{dcId}-1.web.telegram.org"];
    }
}

namespace TgWsProxy.Telegram;

/// <summary>
/// Resolves Telegram WebSocket domains for a DC.
/// </summary>
public static class DcResolver
{
    /// <summary>
    /// Returns candidate Telegram WebSocket domains for the specified DC.
    /// </summary>
    /// <param name="dcId">Telegram data center identifier requested by Telegram Desktop.</param>
    /// <param name="isMedia">Indicates whether Telegram requested a media data center.</param>
    /// <returns>Candidate Telegram WebSocket domains ordered by priority.</returns>
    public static IReadOnlyList<string> GetWebSocketDomains(int dcId, bool isMedia)
    {
        var normalizedDcId = Math.Abs(dcId);
        var isCdnDc = normalizedDcId >= 200;
        var webSocketDcId = GetWebSocketDcId(normalizedDcId);
        var useMediaEndpoint = isMedia || isCdnDc;

        return useMediaEndpoint
            ? [$"kws{webSocketDcId}-1.web.telegram.org", $"kws{webSocketDcId}.web.telegram.org"]
            : [$"kws{webSocketDcId}.web.telegram.org", $"kws{webSocketDcId}-1.web.telegram.org"];
    }

    private static int GetWebSocketDcId(int dcId)
    {
        if (dcId >= 200)
        {
            return dcId - 201;
        }

        return dcId;
    }
}
namespace TgWsProxy.Telegram;

/// <summary>
/// Parsed Telegram obfuscated MTProto initialization packet.
/// </summary>
public sealed record HandshakeResult(
    int DcId,
    bool IsMedia,
    byte[] ProtoTag,
    uint ProtoInt,
    byte[] ClientDecryptPreKeyIv);

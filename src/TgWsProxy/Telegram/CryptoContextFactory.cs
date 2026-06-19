using TgWsProxy.Constants;
using TgWsProxy.Crypto;

namespace TgWsProxy.Telegram;

/// <summary>
/// Builds encryption contexts for client-side and Telegram-side obfuscated MTProto streams.
/// </summary>
public static class CryptoContextFactory
{
    /// <summary>
    /// Creates a crypto context equivalent to the original Python bridge implementation.
    /// </summary>
    public static CryptoContext Create(
        ReadOnlySpan<byte> clientDecryptPreKeyIv,
        ReadOnlySpan<byte> secret,
        ReadOnlySpan<byte> relayInit)
    {
        var clientDecryptPreKey = clientDecryptPreKeyIv[..MtProtoConstants.PreKeyLength];
        var clientDecryptIv = clientDecryptPreKeyIv.Slice(MtProtoConstants.PreKeyLength, MtProtoConstants.IvLength);
        var clientDecryptKey = Hashing.Sha256(clientDecryptPreKey, secret);

        var reversedClientPreKeyIv = clientDecryptPreKeyIv.ToArray();
        Array.Reverse(reversedClientPreKeyIv);

        var clientEncryptKey = Hashing.Sha256(
            reversedClientPreKeyIv.AsSpan(0, MtProtoConstants.PreKeyLength),
            secret);
        var clientEncryptIv = reversedClientPreKeyIv.AsSpan(MtProtoConstants.PreKeyLength, MtProtoConstants.IvLength);

        var clientDecryptor = new AesCtrTransform(clientDecryptKey, clientDecryptIv);
        var clientEncryptor = new AesCtrTransform(clientEncryptKey, clientEncryptIv);

        clientDecryptor.Update(MtProtoConstants.Zero64);

        var telegramEncryptKey = relayInit.Slice(MtProtoConstants.SkipLength, MtProtoConstants.PreKeyLength);
        var telegramEncryptIv = relayInit.Slice(
            MtProtoConstants.SkipLength + MtProtoConstants.PreKeyLength,
            MtProtoConstants.IvLength);

        var reversedRelayPreKeyIv = relayInit.Slice(
            MtProtoConstants.SkipLength,
            MtProtoConstants.PreKeyLength + MtProtoConstants.IvLength).ToArray();
        Array.Reverse(reversedRelayPreKeyIv);

        var telegramDecryptKey = reversedRelayPreKeyIv.AsSpan(0, MtProtoConstants.KeyLength);
        var telegramDecryptIv = reversedRelayPreKeyIv.AsSpan(MtProtoConstants.KeyLength, MtProtoConstants.IvLength);

        var telegramEncryptor = new AesCtrTransform(telegramEncryptKey, telegramEncryptIv);
        var telegramDecryptor = new AesCtrTransform(telegramDecryptKey, telegramDecryptIv);

        telegramEncryptor.Update(MtProtoConstants.Zero64);

        return new CryptoContext(
            clientDecryptor,
            clientEncryptor,
            telegramEncryptor,
            telegramDecryptor);
    }
}

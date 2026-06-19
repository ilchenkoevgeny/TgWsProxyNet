namespace TgWsProxy.Crypto;

/// <summary>
/// Holds independent MTProto obfuscation transforms for client and Telegram DC sides.
/// </summary>
public sealed class CryptoContext : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoContext"/> class.
    /// </summary>
    public CryptoContext(
        AesCtrTransform clientDecryptor,
        AesCtrTransform clientEncryptor,
        AesCtrTransform telegramEncryptor,
        AesCtrTransform telegramDecryptor)
    {
        ClientDecryptor = clientDecryptor;
        ClientEncryptor = clientEncryptor;
        TelegramEncryptor = telegramEncryptor;
        TelegramDecryptor = telegramDecryptor;
    }

    /// <summary>
    /// Decrypts traffic received from Telegram Desktop.
    /// </summary>
    public AesCtrTransform ClientDecryptor { get; }

    /// <summary>
    /// Encrypts traffic sent back to Telegram Desktop.
    /// </summary>
    public AesCtrTransform ClientEncryptor { get; }

    /// <summary>
    /// Encrypts traffic sent to Telegram DC.
    /// </summary>
    public AesCtrTransform TelegramEncryptor { get; }

    /// <summary>
    /// Decrypts traffic received from Telegram DC.
    /// </summary>
    public AesCtrTransform TelegramDecryptor { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        ClientDecryptor.Dispose();
        ClientEncryptor.Dispose();
        TelegramEncryptor.Dispose();
        TelegramDecryptor.Dispose();
    }
}

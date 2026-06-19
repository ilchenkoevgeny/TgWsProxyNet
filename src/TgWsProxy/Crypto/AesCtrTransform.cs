using System.Security.Cryptography;

namespace TgWsProxy.Crypto;

/// <summary>
/// Stateful AES-CTR transform. CTR mode is symmetric, so the same transform is used for encryption and decryption.
/// </summary>
public sealed class AesCtrTransform : IDisposable
{
    private readonly Aes aes;
    private readonly ICryptoTransform encryptor;
    private readonly byte[] counter;
    private readonly byte[] keyStream = new byte[16];
    private int keyStreamOffset = 16;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AesCtrTransform"/> class.
    /// </summary>
    public AesCtrTransform(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        if (iv.Length != 16)
        {
            throw new ArgumentException("CTR IV must contain 16 bytes.", nameof(iv));
        }

        if (key.Length is not 16 and not 24 and not 32)
        {
            throw new ArgumentException("AES key must contain 16, 24 or 32 bytes.", nameof(key));
        }

        counter = iv.ToArray();
        aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key.ToArray();
        encryptor = aes.CreateEncryptor();
    }

    /// <summary>
    /// Transforms the provided bytes and advances the internal CTR state.
    /// </summary>
    public byte[] Update(ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();

        if (data.IsEmpty)
        {
            return [];
        }

        var output = new byte[data.Length];

        for (var i = 0; i < data.Length; i++)
        {
            if (keyStreamOffset >= 16)
            {
                encryptor.TransformBlock(counter, 0, 16, keyStream, 0);
                IncrementCounterBigEndian(counter);
                keyStreamOffset = 0;
            }

            output[i] = (byte)(data[i] ^ keyStream[keyStreamOffset]);
            keyStreamOffset++;
        }

        return output;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        encryptor.Dispose();
        aes.Dispose();
        disposed = true;
    }

    private static void IncrementCounterBigEndian(Span<byte> value)
    {
        for (var i = value.Length - 1; i >= 0; i--)
        {
            value[i]++;

            if (value[i] != 0)
            {
                break;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}

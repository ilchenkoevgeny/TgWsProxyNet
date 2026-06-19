namespace TgWsProxy.Constants;

/// <summary>
/// Constants used by Telegram MTProto obfuscated transport.
/// </summary>
public static class MtProtoConstants
{
    public const int HandshakeLength = 64;
    public const int SkipLength = 8;
    public const int PreKeyLength = 32;
    public const int KeyLength = 32;
    public const int IvLength = 16;
    public const int ProtoTagPosition = 56;
    public const int DcIndexPosition = 60;

    public static readonly byte[] Zero64 = new byte[64];

    public static readonly byte[] ProtoTagAbridged = [0xEF, 0xEF, 0xEF, 0xEF];
    public static readonly byte[] ProtoTagIntermediate = [0xEE, 0xEE, 0xEE, 0xEE];
    public static readonly byte[] ProtoTagSecure = [0xDD, 0xDD, 0xDD, 0xDD];

    public const uint ProtoAbridged = 0xEFEFEFEF;
    public const uint ProtoIntermediate = 0xEEEEEEEE;
    public const uint ProtoPaddedIntermediate = 0xDDDDDDDD;

    public static readonly byte[][] ReservedStarts =
    [
        [0x48, 0x45, 0x41, 0x44],
        [0x50, 0x4F, 0x53, 0x54],
        [0x47, 0x45, 0x54, 0x20],
        [0xEE, 0xEE, 0xEE, 0xEE],
        [0xDD, 0xDD, 0xDD, 0xDD],
        [0x16, 0x03, 0x01, 0x02]
    ];

    public static readonly byte[] ReservedContinue = [0x00, 0x00, 0x00, 0x00];
}

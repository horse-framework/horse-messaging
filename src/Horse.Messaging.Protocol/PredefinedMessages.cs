namespace Horse.Messaging.Protocol;

/// <summary>
/// Predefined messages for Horse Protocol
/// </summary>
public static class PredefinedMessages
{
    /// <summary>
    /// "HORSE/30" as bytes, protocol handshaking message
    /// </summary>
    public static readonly byte[] PROTOCOL_BYTES_V3 = "HORSE/30"u8.ToArray();

    /// <summary>
    /// "HORSE/40" as bytes, protocol handshaking message
    /// </summary>
    public static readonly byte[] PROTOCOL_BYTES_V4 = "HORSE/40"u8.ToArray();

    /// <summary>
    /// PING message for Horse "0x89, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00"
    /// </summary>
    public static readonly byte[] PING = [0x89, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    /// <summary>
    /// PONG message for Horse "0x8A, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00"
    /// </summary>
    public static readonly byte[] PONG = [0x8A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
}
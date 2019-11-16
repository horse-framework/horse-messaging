namespace Twino.Protocols.WebSocket
{
    internal static class PredefinedMessages
    {
        internal static readonly byte[] PING = {0x89, 0x00};
        internal static readonly byte[] PONG = {0x8A, 0x00};
    }
}
namespace Twino.Protocols.TMQ
{
    internal static class PredefinedMessages
    {
        public static readonly byte HELLO_BYTE = 0x01;

        internal static readonly byte[] PING = {0x89, 0xFF, 0x00, 0x00, 0x00, 0x00};
        internal static readonly byte[] PONG = {0x8A, 0xFF, 0x00, 0x00, 0x00, 0x00};
    }
}
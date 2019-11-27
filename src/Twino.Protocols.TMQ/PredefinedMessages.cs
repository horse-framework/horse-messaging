using System.Text;

namespace Twino.Protocols.TMQ
{
    public static class PredefinedMessages
    {
        public static readonly byte[] PROTOCOL_BYTES = Encoding.ASCII.GetBytes("TMQ/1.01");

        public static readonly byte[] PING = {0x89, 0xFF, 0x00, 0x00, 0x00, 0x00};
        public static readonly byte[] PONG = {0x8A, 0xFF, 0x00, 0x00, 0x00, 0x00};
    }
}
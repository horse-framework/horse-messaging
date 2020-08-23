using System.Text;

namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// Predefined messages for TMQ Protocol
    /// </summary>
    public static class PredefinedMessages
    {
        /// <summary>
        /// "TMQ/1.01" as bytes, protocol handshaking message
        /// </summary>
        public static readonly byte[] PROTOCOL_BYTES_V1 = Encoding.ASCII.GetBytes("TMQ/1.01");
        
        /// <summary>
        /// "TMQ/1.01" as bytes, protocol handshaking message
        /// </summary>
        public static readonly byte[] PROTOCOL_BYTES_V2 = Encoding.ASCII.GetBytes("TMQP/2.0");

        /// <summary>
        /// PING message for TMQ "0x89, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00"
        /// </summary>
        public static readonly byte[] PING = { 0x89, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        /// <summary>
        /// PONG message for TMQ "0x8A, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00"
        /// </summary>
        public static readonly byte[] PONG = { 0x8A, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    }
}
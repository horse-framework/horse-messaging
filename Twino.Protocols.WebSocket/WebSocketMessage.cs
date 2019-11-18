using System.IO;
using System.Text;

namespace Twino.Protocols.WebSocket
{
    /// <summary>
    /// WebSocket Protocol OP Codes
    /// </summary>
    public enum SocketOpCode : byte
    {
        /// <summary>
        /// When the data consists of multiple packages, until the last package, all packages must have continue definition
        /// </summary>
        Continue = 0x00,

        /// <summary>
        /// Sending text data definition
        /// </summary>
        UTF8 = 0x01,

        /// <summary>
        /// Sending binary data definition
        /// </summary>
        Binary = 0x02,

        /// <summary>
        /// Terminating connection definition
        /// </summary>
        Terminate = 0x08,

        /// <summary>
        /// Sending ping request definition
        /// </summary>
        Ping = 0x09,

        /// <summary>
        /// Sending response of the ping definition
        /// </summary>
        Pong = 0x0A
    }

    public class WebSocketMessage
    {
        public SocketOpCode OpCode { get; set; }
        public bool Masking { get; set; }
        public byte[] Mask { get; set; }
        public ulong Length { get; set; }
        public MemoryStream Content { get; set; }

        public static WebSocketMessage FromString(string message)
        {
            return new WebSocketMessage
                   {
                       OpCode = SocketOpCode.UTF8,
                       Content = new MemoryStream(Encoding.UTF8.GetBytes(message))
                   };
        }
    }
}
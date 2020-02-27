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

    /// <summary>
    /// WebSocket Message
    /// </summary>
    public class WebSocketMessage
    {
        /// <summary>
        /// WebSocket protocol OpCode (message type)
        /// </summary>
        public SocketOpCode OpCode { get; set; }

        /// <summary>
        /// True, if message is masking with a key
        /// </summary>
        public bool Masking { get; set; }

        /// <summary>
        /// If message is masking, 4 bytes key value. Otherwise null.
        /// </summary>
        public byte[] Mask { get; set; }

        /// <summary>
        /// Message length
        /// </summary>
        public long Length => Content != null ? Content.Length : 0;

        /// <summary>
        /// Message content stream
        /// </summary>
        public MemoryStream Content { get; set; }

        /// <summary>
        /// Creates new message from string
        /// </summary>
        public static WebSocketMessage FromString(string message)
        {
            return new WebSocketMessage
            {
                OpCode = SocketOpCode.UTF8,
                Content = new MemoryStream(Encoding.UTF8.GetBytes(message))
            };
        }

        /// <summary>
        /// Reads message content as UTF-8 string
        /// </summary>
        public override string ToString()
        {
            if (Content != null)
                return Encoding.UTF8.GetString(Content.ToArray());

            return string.Empty;
        }
    }
}
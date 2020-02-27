using System;
using System.Text;

namespace Twino.Protocols.WebSocket
{
    /// <summary>
    /// Predefined messages for Websocket protocol
    /// </summary>
    public static class PredefinedMessages
    {
        /// <summary>
        /// Websocket PING message 0x89 0x00
        /// </summary>
        public static readonly byte[] PING = { 0x89, 0x00 };

        /// <summary>
        /// Websocket PONG message 0x8A 0x00
        /// </summary>
        public static readonly byte[] PONG = { 0x8A, 0x00 };

        /// <summary>
        /// HTTP Header, "Sec-WebSocket-Key"
        /// </summary>
        public const string WEBSOCKET_KEY = "Sec-WebSocket-Key";

        /// <summary>
        /// "HTTP/1.1 " as bytes
        /// </summary>
        public static readonly ReadOnlyMemory<byte> HTTP_VERSION = Encoding.ASCII.GetBytes("HTTP/1.1 ");

        /// <summary>
        /// "Server: twino\r\n" as bytes
        /// </summary>
        public static readonly ReadOnlyMemory<byte> SERVER_CRLF = Encoding.ASCII.GetBytes("Server: twino\r\n");

        /// <summary>
        /// "Connection: Upgrade\r\n" as bytes
        /// </summary>
        public static readonly ReadOnlyMemory<byte> CONNECTION_UPGRADE_CRLF = Encoding.ASCII.GetBytes("Connection: Upgrade\r\n");

        /// <summary>
        /// "HTTP/1.1 101 Switching Protocols\r\n" as bytes
        /// </summary>
        public static ReadOnlyMemory<byte> WEBSOCKET_101_SWITCHING_PROTOCOLS_CRLF = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\n");

        /// <summary>
        /// "Upgrade: websocket\r\n" as bytes
        /// </summary>
        public static ReadOnlyMemory<byte> UPGRADE_WEBSOCKET_CRLF = Encoding.ASCII.GetBytes("Upgrade: websocket\r\n");

        /// <summary>
        /// "Sec-WebSocket-Accept: " as bytes
        /// </summary>
        public static ReadOnlyMemory<byte> SEC_WEB_SOCKET_COLON = Encoding.ASCII.GetBytes("Sec-WebSocket-Accept: ");

        /// <summary>
        /// "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
        /// </summary>
        public const string WEBSOCKET_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    }
}
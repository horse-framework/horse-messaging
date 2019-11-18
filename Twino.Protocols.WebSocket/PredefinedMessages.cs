using System;
using System.Text;

namespace Twino.Protocols.WebSocket
{
    internal static class PredefinedMessages
    {
        internal static readonly byte[] PING = {0x89, 0x00};
        internal static readonly byte[] PONG = {0x8A, 0x00};

        public const string WEBSOCKET_KEY = "Sec-WebSocket-Key";

        internal static readonly ReadOnlyMemory<byte> HTTP_VERSION = Encoding.ASCII.GetBytes("HTTP/1.1 ");
        internal static readonly ReadOnlyMemory<byte> SERVER_CRLF = Encoding.ASCII.GetBytes("Server: twino\r\n");
        internal static readonly ReadOnlyMemory<byte> CONNECTION_UPGRADE_CRLF = Encoding.ASCII.GetBytes("Connection: Upgrade\r\n");
        internal static ReadOnlyMemory<byte> WEBSOCKET_101_SWITCHING_PROTOCOLS_CRLF = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\n");
        internal static ReadOnlyMemory<byte> UPGRADE_WEBSOCKET_CRLF = Encoding.ASCII.GetBytes("Upgrade: websocket\r\n");
        internal static ReadOnlyMemory<byte> SEC_WEB_SOCKET_COLON = Encoding.ASCII.GetBytes("Sec-WebSocket-Accept: ");

        public const string WEBSOCKET_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    }
}
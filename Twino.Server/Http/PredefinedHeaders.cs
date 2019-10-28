using System;
using System.Text;

namespace Twino.Server.Http
{
    public static class PredefinedHeaders
    {
        internal static readonly ReadOnlyMemory<byte> HTTP_VERSION = Encoding.ASCII.GetBytes("HTTP/1.1 ");
        internal static readonly ReadOnlyMemory<byte> SERVER_CRLF = Encoding.ASCII.GetBytes("Server: twino\r\n");
        internal static readonly ReadOnlyMemory<byte> CONTENT_LENGTH_COLON = Encoding.ASCII.GetBytes("Content-Length: ");
        internal static readonly ReadOnlyMemory<byte> CONNECTION_KEEP_ALIVE_CRLF = Encoding.ASCII.GetBytes("Connection: keep-alive\r\n");
        internal static readonly ReadOnlyMemory<byte> CONNECTION_CLOSE_CRLF = Encoding.ASCII.GetBytes("Connection: close\r\n");
        internal static readonly ReadOnlyMemory<byte> CHARSET_UTF8_CRLF = Encoding.ASCII.GetBytes(";charset=UTF-8\r\n");
        internal static readonly ReadOnlyMemory<byte> CONTENT_TYPE_COLON = Encoding.ASCII.GetBytes("Content-Type: ");
        
        internal static readonly ReadOnlyMemory<byte> ENCODING_GZIP_CRLF = Encoding.ASCII.GetBytes("Content-Encoding: gzip\r\n");
        internal static readonly ReadOnlyMemory<byte> ENCODING_BR_CRLF = Encoding.ASCII.GetBytes("Content-Encoding: br\n");

        internal static ReadOnlyMemory<byte> SERVER_TIME_CRLF = Encoding.ASCII.GetBytes("Date: " + DateTime.UtcNow.ToString("R") + "\r\n");
    }
}
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Twino.Core.Http
{
    /// <summary>
    /// HTTP Header known key and value list.
    /// Created to avoid mistyping these values.
    /// </summary>
    public static class HttpHeaders
    {
        public const string HOST = "Host";
        public const string ORIGIN = "Origin";
        public const string SERVER = "Server";
        public const string DATE = "Date";
        public const string HTTP_VERSION = "HTTP/1.1";
        public const string CONTENT_TYPE = "Content-Type";
        public const string LOCATION = "Location";
        public const string CONTENT_ENCODING = "Content-Encoding";
        public const string CONTENT_LENGTH = "Content-Length";
        public const string ACCEPT_ENCODING = "Accept-Encoding";
        public const string ACCEPT_LANGUAGE = "Accept-Language";
        public const string UPGRADE = "Upgrade";
        public const string PRAGMA = "Pragma";
        public const string CACHE_CONTROL = "Cache-Control";
        public const string CONNECTION = "Connection";
        public const string WEBSOCKET_VERSION = "Sec-WebSocket-Version";
        public const string WEBSOCKET_KEY = "Sec-WebSocket-Key";
        public const string WEBSOCKET_EXTENSIONS = "Sec-WebSocket-Extensions";
        public const string WEBSOCKET_ACCEPT = "Sec-WebSocket-Accept";
        public const string AUTHORIZATION = "Authorization";
        public const string BEARER = "Bearer";
        
        public const string ACCESS_CONTROL_ALLOW_CREDENTIALS = "Access-Control-Allow-Credentials";
        public const string ACCESS_CONTROL_ALLOW_ORIGIN = "Access-Control-Allow-Origin";
        public const string ACCESS_CONTROL_ALLOW_HEADERS = "Access-Control-Allow-Headers";
        public const string ACCESS_CONTROL_ALLOW_METHODS = "Access-Control-Allow-Methods";
        public const string ACCESS_CONTROL_MAX_AGE = "Access-Control-Max-Age";

        public const string HTTP_GET = "GET";
        public const string HTTP_POST = "POST";
        public const string HTTP_PUT = "PUT";
        public const string HTTP_PATCH = "PATCH";
        public const string HTTP_DELETE = "DELETE";
        public const string HTTP_OPTIONS = "OPTIONS";
        public const string HTTP_HEAD = "HEAD";

        public const string VALUE_SERVER = "twino";
        public const string VALUE_CHARSET_UTF8 = "charset=UTF-8";
        public const string VALUE_GZIP = "gzip";
        public const string VALUE_BROTLI = "br";
        public const string VALUE_GZIP_DEFLATE_BR = "gzip, deflate, br";
        public const string VALUE_WEBSOCKET = "websocket";
        public const string VALUE_WEBSOCKET_VERSION = "13";
        public const string VALUE_ACCEPT_EN = "en-US,en;q=0.9";
        public const string VALUE_NO_CACHE = "no-cache";
        public const string VALUE_KEEP_ALIVE = "keep-alive";
        public const string VALUE_CLOSE = "close";
        public const string VALUE_CLOSED = "Closed";
        public const string VALUE_TIMEOUT = "timeout";
        public const string VALUE_MAX = "max";
        public const string VALUE_WEBSOCKET_EXTENSIONS = "permessage-deflate; client_max_window_bits";


        public const string WEBSOCKET_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        /// <summary>
        /// Creates new header line like "Key: value + [\r\n]"
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Create(string key, int value)
        {
            return Create(key, value.ToString());
        }

        /// <summary>
        /// Creates new header line like "line + [\r\n]"
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Create(string line)
        {
            return line + "\r\n";
        }

        /// <summary>
        /// Creates new header line like "Key: value + [\r\n]"
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Create(string key, string value)
        {
            return string.Concat(key, ": ", value, "\r\n"); // key + ": " + value + "\r\n";
        }

        /// <summary>
        /// Creates new header line like "Key: value; other_value; other_value... + [\r\n]"
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Create(string key, string value, params string[] otherValues)
        {
            string total = value;
            foreach (string other in otherValues)
                total += "; " + other;

            return key + ": " + total + "\r\n";
        }
    }
}
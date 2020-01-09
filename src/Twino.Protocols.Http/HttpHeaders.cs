using System.Runtime.CompilerServices;

namespace Twino.Protocols.Http
{
    /// <summary>
    /// HTTP Header known key and value list.
    /// Created to avoid mistyping these values.
    /// </summary>
    public static class HttpHeaders
    {
        /// <summary>
        /// "Host"
        /// </summary>
        public const string HOST = "Host";
        
        /// <summary>
        /// "Origin"
        /// </summary>
        public const string ORIGIN = "Origin";
        
        /// <summary>
        /// "Server"
        /// </summary>
        public const string SERVER = "Server";
        
        /// <summary>
        /// "Date"
        /// </summary>
        public const string DATE = "Date";
        
        /// <summary>
        /// "HTTP/1.1"
        /// </summary>
        public const string HTTP_VERSION = "HTTP/1.1";
        
        /// <summary>
        /// "Content-Type"
        /// </summary>
        public const string CONTENT_TYPE = "Content-Type";
        
        /// <summary>
        /// "Location"
        /// </summary>
        public const string LOCATION = "Location";
        
        /// <summary>
        /// "Content-Encoding"
        /// </summary>
        public const string CONTENT_ENCODING = "Content-Encoding";
        
        /// <summary>
        /// "Content-Length"
        /// </summary>
        public const string CONTENT_LENGTH = "Content-Length";
        
        /// <summary>
        /// "Content-Disposition"
        /// </summary>
        public const string CONTENT_DISPOSITION = "Content-Disposition";
        
        /// <summary>
        /// "Accept-Encoding"
        /// </summary>
        public const string ACCEPT_ENCODING = "Accept-Encoding";
        
        /// <summary>
        /// "Accept-Language"
        /// </summary>
        public const string ACCEPT_LANGUAGE = "Accept-Language";
        
        /// <summary>
        /// "Upgrade"
        /// </summary>
        public const string UPGRADE = "Upgrade";
        
        /// <summary>
        /// "Pragma"
        /// </summary>
        public const string PRAGMA = "Pragma";
        
        /// <summary>
        /// "Cache-Control"
        /// </summary>
        public const string CACHE_CONTROL = "Cache-Control";
        
        /// <summary>
        /// "Connection"
        /// </summary>
        public const string CONNECTION = "Connection";
        
        /// <summary>
        /// "Sec-WebSocket-Version"
        /// </summary>
        public const string WEBSOCKET_VERSION = "Sec-WebSocket-Version";
        
        /// <summary>
        /// "Sec-WebSocket-Key"
        /// </summary>
        public const string WEBSOCKET_KEY = "Sec-WebSocket-Key";
        
        /// <summary>
        /// "Sec-WebSocket-Extensions"
        /// </summary>
        public const string WEBSOCKET_EXTENSIONS = "Sec-WebSocket-Extensions";
        
        /// <summary>
        /// "Sec-WebSocket-Accept"
        /// </summary>
        public const string WEBSOCKET_ACCEPT = "Sec-WebSocket-Accept";
        
        /// <summary>
        /// "Authorization"
        /// </summary>
        public const string AUTHORIZATION = "Authorization";
        
        /// <summary>
        /// "Bearer"
        /// </summary>
        public const string BEARER = "Bearer";
        
        /// <summary>
        /// "Access-Control-Allow-Credentials"
        /// </summary>
        public const string ACCESS_CONTROL_ALLOW_CREDENTIALS = "Access-Control-Allow-Credentials";
        
        /// <summary>
        /// "Access-Control-Allow-Origin"
        /// </summary>
        public const string ACCESS_CONTROL_ALLOW_ORIGIN = "Access-Control-Allow-Origin";
        
        /// <summary>
        /// "Access-Control-Allow-Headers"
        /// </summary>
        public const string ACCESS_CONTROL_ALLOW_HEADERS = "Access-Control-Allow-Headers";
        
        /// <summary>
        /// "Access-Control-Allow-Methods"
        /// </summary>
        public const string ACCESS_CONTROL_ALLOW_METHODS = "Access-Control-Allow-Methods";
        
        /// <summary>
        /// "Access-Control-Max-Age"
        /// </summary>
        public const string ACCESS_CONTROL_MAX_AGE = "Access-Control-Max-Age";

        /// <summary>
        /// "GET"
        /// </summary>
        public const string HTTP_GET = "GET";
        
        /// <summary>
        /// "POST"
        /// </summary>
        public const string HTTP_POST = "POST";
        
        /// <summary>
        /// "PUT"
        /// </summary>
        public const string HTTP_PUT = "PUT";
        
        /// <summary>
        /// "PATCH"
        /// </summary>
        public const string HTTP_PATCH = "PATCH";
        
        /// <summary>
        /// "DELETE"
        /// </summary>
        public const string HTTP_DELETE = "DELETE";
        
        /// <summary>
        /// "OPTIONS"
        /// </summary>
        public const string HTTP_OPTIONS = "OPTIONS";
        
        /// <summary>
        /// "HEAD"
        /// </summary>
        public const string HTTP_HEAD = "HEAD";

        /// <summary>
        /// "multipart/form-data"
        /// </summary>
        public const string MULTIPART_FORM_DATA = "multipart/form-data";
        
        /// <summary>
        /// "application/x-www-form-urlencoded"
        /// </summary>
        public const string APPLICATION_FORM_URLENCODED = "application/x-www-form-urlencoded";
        
        /// <summary>
        /// "application/octet-stream"
        /// </summary>
        public const string APPLICATION_OCTET_STREAM = "application/octet-stream";
        
        /// <summary>
        /// "multipart/mixed"
        /// </summary>
        public const string MULTIPART_MIXED = "multipart/mixed";

        /// <summary>
        /// "twino"
        /// </summary>
        public const string VALUE_SERVER = "twino";
        
        /// <summary>
        /// "charset=UTF-8"
        /// </summary>
        public const string VALUE_CHARSET_UTF8 = "charset=UTF-8";
        
        /// <summary>
        /// "gzip"
        /// </summary>
        public const string VALUE_GZIP = "gzip";
        
        /// <summary>
        /// "br"
        /// </summary>
        public const string VALUE_BROTLI = "br";
        
        /// <summary>
        /// "gzip, deflate, br"
        /// </summary>
        public const string VALUE_GZIP_DEFLATE_BR = "gzip, deflate, br";
        
        /// <summary>
        /// "websocket"
        /// </summary>
        public const string VALUE_WEBSOCKET = "websocket";
        
        /// <summary>
        /// "13"
        /// </summary>
        public const string VALUE_WEBSOCKET_VERSION = "13";
        
        /// <summary>
        /// "en-US,en;q=0.9"
        /// </summary>
        public const string VALUE_ACCEPT_EN = "en-US,en;q=0.9";
        
        /// <summary>
        /// "no-cache"
        /// </summary>
        public const string VALUE_NO_CACHE = "no-cache";
        
        /// <summary>
        /// "keep-alive"
        /// </summary>
        public const string VALUE_KEEP_ALIVE = "keep-alive";
        
        /// <summary>
        /// "close"
        /// </summary>
        public const string VALUE_CLOSE = "close";
        
        /// <summary>
        /// "Closed"
        /// </summary>
        public const string VALUE_CLOSED = "Closed";
        
        /// <summary>
        /// "timeout"
        /// </summary>
        public const string VALUE_TIMEOUT = "timeout";
        
        /// <summary>
        /// "max"
        /// </summary>
        public const string VALUE_MAX = "max";
        
        /// <summary>
        /// "permessage-deflate; client_max_window_bits"
        /// </summary>
        public const string VALUE_WEBSOCKET_EXTENSIONS = "permessage-deflate; client_max_window_bits";
        
        /// <summary>
        /// "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
        /// </summary>
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
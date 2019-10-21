namespace Twino.Core.Http
{
    /// <summary>
    /// HTTP Header known key and value list.
    /// Created to avoid mistyping these values.
    /// </summary>
    public static class HttpHeaders
    {
        public const string HOST = "host";
        public const string ORIGIN = "origin";
        public const string SERVER = "server";
        public const string HTTP_VERSION = "HTTP/1.1";
        public const string CONTENT_TYPE = "content-Type";
        public const string LOCATION = "Location";
        public const string CONTENT_ENCODING = "content-encoding";
        public const string CONTENT_LENGTH = "content-length";
        public const string ACCEPT_ENCODING = "accept-encoding";
        public const string ACCEPT_LANGUAGE = "accept-language";
        public const string UPGRADE = "upgrade";
        public const string PRAGMA = "pragma";
        public const string CACHE_CONTROL = "cache-control";
        public const string CONNECTION = "connection";
        public const string WEBSOCKET_VERSION = "Sec-websocket-version";
        public const string WEBSOCKET_KEY = "sec-websocket-key";
        public const string WEBSOCKET_EXTENSIONS = "sec-websocket-extensions";
        public const string WEBSOCKET_ACCEPT = "sec-websocket-accept";
        public const string AUTHORIZATION = "authorization";
        public const string BEARER = "Bearer";
        public const string ACCESS_CONTROL_ALLOW_ORIGIN = "access-control-allow-origin";

        public const string LCASE_HOST = "host";
        public const string LCASE_ACCEPT_ENCODING = "accept-encoding";
        public const string LCASE_WEBSOCKET_KEY = "sec-websocket-key";
        public const string LCASE_CONTENT_TYPE = "content-type";
        public const string LCASE_CONTENT_LENGTH = "content-length";

        public const string HTTP_GET = "GET";
        public const string HTTP_POST = "POST";
        public const string HTTP_PUT = "PUT";
        public const string HTTP_PATCH = "PATCH";
        public const string HTTP_DELETE = "DELETE";
        public const string HTTP_OPTIONS = "OPTIONS";
        public const string HTTP_HEAD = "HEAD";
        public const string LCASE_HTTP_GET = "get";
        public const string LCASE_HTTP_POST = "post";
        public const string LCASE_HTTP_PUT = "put";
        public const string LCASE_HTTP_PATCH = "patch";
        public const string LCASE_HTTP_DELETE = "delete";
        public const string LCASE_HTTP_OPTIONS = "options";
        public const string LCASE_HTTP_HEAD = "head";

        public const string VALUE_SERVER = "twino";
        public const string VALUE_CHARSET_UTF8 = "charset=utf8";
        public const string VALUE_GZIP = "gzip";
        public const string VALUE_BROTLI = "br";
        public const string VALUE_GZIP_DEFLATE_BR = "gzip, deflate, br";
        public const string VALUE_WEBSOCKET = "websocket";
        public const string VALUE_WEBSOCKET_VERSION = "13";
        public const string VALUE_ACCEPT_EN = "en-US,en;q=0.9";
        public const string VALUE_NO_CACHE = "no-cache";
        public const string VALUE_KEEP_ALIVE = "keep-alive";
        public const string VALUE_CLOSE = "close";
        public const string VALUE_WEBSOCKET_EXTENSIONS = "permessage-deflate; client_max_window_bits";

        public const string WEBSOCKET_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        /// <summary>
        /// Creates new header line like "Key: value + [\r\n]"
        /// </summary>
        public static string Create(string key, int value)
        {
            return Create(key, value.ToString());
        }

        /// <summary>
        /// Creates new header line like "line + [\r\n]"
        /// </summary>
        public static string Create(string line)
        {
            return line + "\r\n";
        }

        /// <summary>
        /// Creates new header line like "Key: value + [\r\n]"
        /// </summary>
        public static string Create(string key, string value)
        {
            return key + ": " + value + "\r\n";
        }

        /// <summary>
        /// Creates new header line like "Key: value; other_value; other_value... + [\r\n]"
        /// </summary>
        public static string Create(string key, string value, params string[] otherValues)
        {
            string total = value;
            foreach (string other in otherValues)
                total += "; " + other;

            return key + ": " + total + "\r\n";
        }
    }
}
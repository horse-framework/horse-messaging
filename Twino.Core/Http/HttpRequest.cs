using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("Twino.Server")]

namespace Twino.Core.Http
{
    /// <summary>
    /// HttpRequest for HttpServer object.
    /// It is used both WebSocket and Http requests
    /// </summary>
    public class HttpRequest
    {
        /// <summary>
        /// Http Method name as uppercase (ex: GET, POST, PUT)
        /// </summary>
        public string Method { get; internal set; }

        /// <summary>
        /// Requested host name
        /// </summary>
        public string Host { get; internal set; }

        /// <summary>
        /// Request path. Does not include protocol or hostname. Starts with "/"
        /// </summary>
        public string Path { get; internal set; }

        /// <summary>
        /// If true, request is secure (with SSL)
        /// </summary>
        public bool IsHttps { get; internal set; }

        /// <summary>
        /// Requester client IP Address
        /// </summary>
        public string IpAddress { get; internal set; }

        /// <summary>
        /// If true, request has web socket key header and it's websocket request
        /// </summary>
        public bool IsWebSocket { get; set; }

        /// <summary>
        /// For web socket requests, includes key for websocket client. Otherwise it's null
        /// </summary>
        public string WebSocketKey { get; set; }

        /// <summary>
        /// Client's accepted encodings.
        /// If this propert includes "gzip" server response will be gzipped.
        /// Otherwise server will response as plain text with content-length.
        /// Twino server does not support truncated length format
        /// </summary>
        public string AcceptEncoding { get; set; }

        /// <summary>
        /// Request content
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Length of request content
        /// </summary>
        public int ContentLength { get; internal set; }

        /// <summary>
        /// True if request has Content-Length header
        /// </summary>
        internal bool ContentLengthSpecified { get; set; }

        /// <summary>
        /// Request Content Type (form, json etc)
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// All other headers as key and value
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Response of the request
        /// </summary>
        public HttpResponse Response { get; internal set; }

        /// <summary>
        /// Parses Path extension and returns query string key value pairs
        /// </summary>
        public Dictionary<string, string> GetQueryStringValues()
        {
            Dictionary<string, string> items = new Dictionary<string, string>();

            int start = Path.IndexOf('?');
            if (start < 0)
                return items;

            string[] pairs = Path.Substring(start + 1).Split('&');
            foreach (string pair in pairs)
            {
                string[] key_value = pair.Split('=');
                if (key_value.Length != 2)
                    continue;

                string key = key_value[0];
                string value = HtmlEncoder.HtmlDecode(key_value[1], Encoding.UTF8);

                if (items.ContainsKey(key))
                    items[key] += "," + value;
                else
                    items.Add(key, value);
            }

            return items;
        }

        /// <summary>
        /// Parses request content and returns form key value pairs
        /// </summary>
        public Dictionary<string, string> GetFormValues()
        {
            Dictionary<string, string> items = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(Content))
                return items;

            string[] pairs = Content.Split('&');
            foreach (string pair in pairs)
            {
                string[] key_value = pair.Split('=');
                if (key_value.Length != 2)
                    continue;

                string key = key_value[0];
                string value = HtmlEncoder.HtmlDecode(key_value[1], Encoding.UTF8);

                if (items.ContainsKey(key))
                    items[key] = "," + value;
                else
                    items.Add(key, value);
            }

            return items;
        }

        /// <summary>
        /// Returns only path. If path includes query string, result doesn't contains query string data
        /// </summary>
        public string GetOnlyPath()
        {
            int qstr_index = Path.IndexOf('?');
            if (qstr_index < 0)
                return Path;

            return Path.Substring(0, qstr_index);
        }
    }
}
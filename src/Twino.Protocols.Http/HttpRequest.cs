using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Twino.Protocols.Http.Forms;

[assembly: InternalsVisibleTo("Twino.Server")]
[assembly: InternalsVisibleTo("Twino.Client.WebSocket")]

namespace Twino.Protocols.Http
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
        /// If request needs to switch protocol, this value is the requested protocol name
        /// </summary>
        public string Upgrade { get; internal set; }

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
        /// Request content stream
        /// </summary>
        public MemoryStream ContentStream { get; set; }

        /// <summary>
        /// Length of request content
        /// </summary>
        public int ContentLength { get; internal set; }

        /// <summary>
        /// Request Content Type (form, json etc)
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Request query string key and values
        /// </summary>
        public Dictionary<string, string> QueryString { get; internal set; }

        /// <summary>
        /// Request form data
        /// </summary>
        public Dictionary<string, string> Form { get; internal set; }

        /// <summary>
        /// Request files
        /// </summary>
        public IEnumerable<IFormFile> Files { get; internal set; }

        /// <summary>
        /// All other headers as key and value
        /// </summary>
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Response of the request
        /// </summary>
        public HttpResponse Response { get; internal set; }

        /// <summary>
        /// Full querystring data, such as "a=1&amp;b=2&amp;c=3..."
        /// </summary>
        internal string QueryStringData { get; set; }

        /// <summary>
        /// Multipart form data root boundary
        /// </summary>
        internal string Boundary { get; set; }

        /// <summary>
        /// True if request has Content-Length header
        /// </summary>
        internal bool ContentLengthSpecified { get; set; }
    }
}
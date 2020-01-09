using System;
using Twino.Mvc.Controllers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Twino.Core;

namespace Twino.Mvc.Results
{
    /// <summary>
    /// HTML Action result
    /// </summary>
    public class HtmlResult : IActionResult
    {
        /// <summary>
        /// Result HTTP Status code
        /// </summary>
        public HttpStatusCode Code { get; set; }

        /// <summary>
        /// Result content type (such as application/json, text/xml, text/plain)
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// Result content body
        /// </summary>
        public Stream Stream { get; }

        /// <summary>
        /// Additional custom headers with key and value
        /// </summary>
        public Dictionary<string, string> Headers { get; }

        /// <summary>
        /// Creates new HTML Result from HTML string
        /// </summary>
        public HtmlResult(string content)
        {
            Code = HttpStatusCode.OK;
            ContentType = ContentTypes.TEXT_HTML;
            Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>
        /// Creates new HTML result from byte array
        /// </summary>
        public HtmlResult(byte[] bytes)
        {
            Code = HttpStatusCode.OK;
            ContentType = ContentTypes.TEXT_HTML;
            Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            Stream = new MemoryStream(bytes);
        }
    }
}
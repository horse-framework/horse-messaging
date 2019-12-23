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
    /// STRING Action result
    /// </summary>
    public class StringResult : IActionResult
    {
        /// <summary>
        /// Result HTTP Status code
        /// </summary>
        public HttpStatusCode Code { get; set; }

        /// <summary>
        /// Result content type (such as application/json, text/xml, text/plain)
        /// </summary>
        public string ContentType { get; private set; }

        /// <summary>
        /// Result content body
        /// </summary>
        public Stream Stream { get; private set; }

        /// <summary>
        /// Additional custom headers with key and value
        /// </summary>
        public Dictionary<string, string> Headers { get; }

        public StringResult(string content)
        {
            Code = HttpStatusCode.OK;
            ContentType = ContentTypes.PLAIN_TEXT;
            Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        }
    }
}
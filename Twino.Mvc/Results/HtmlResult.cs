using System;
using Twino.Mvc.Controllers;
using System.Collections.Generic;
using System.Net;
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
        public string Content { get; }

        /// <summary>
        /// Additional custom headers with key and value
        /// </summary>
        public Dictionary<string, string> Headers { get; }

        public HtmlResult(string content)
        {
            Code = HttpStatusCode.OK;
            ContentType = ContentTypes.TEXT_HTML;
            Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            Content = content;
        }
    }
}

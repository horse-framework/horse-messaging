using System.Collections.Generic;
using System.Net;

namespace Twino.Mvc.Controllers
{
    /// <summary>
    /// Twino MVC Action result interface
    /// </summary>
    public interface IActionResult
    {
        /// <summary>
        /// Result HTTP Status code
        /// </summary>
        HttpStatusCode Code { get; }

        /// <summary>
        /// Result content type (such as json, xml, plain text, html)
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// Result content body
        /// </summary>
        string Content { get; }

        /// <summary>
        /// Additional custom headers with key and value
        /// </summary>
        Dictionary<string, string> Headers { get; }
    }
}

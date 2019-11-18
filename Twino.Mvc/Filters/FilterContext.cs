using Twino.Mvc.Controllers;
using System.Security.Claims;
using Twino.Core;
using Twino.Protocols.Http;

namespace Twino.Mvc.Filters
{
    /// <summary>
    /// Context data for filter objects.
    /// </summary>
    public class FilterContext
    {
        /// <summary>
        /// Underlying HTTP Server
        /// </summary>
        public ITwinoServer Server { get; internal set; }

        /// <summary>
        /// HTTP Request
        /// </summary>
        public HttpRequest Request { get; internal set; }

        /// <summary>
        /// HTTP Response
        /// </summary>
        public HttpResponse Response { get; internal set; }

        /// <summary>
        /// Result for Request / Response lifecycle
        /// </summary>
        public IActionResult Result { get; set; }

        /// <summary>
        /// Get Claims for user associated for executing request
        /// </summary>
        public ClaimsPrincipal User { get; internal set; }

    }
}

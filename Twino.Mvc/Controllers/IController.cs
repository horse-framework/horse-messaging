using System.Security.Claims;
using Twino.Core;
using Twino.Protocols.Http;

namespace Twino.Mvc.Controllers
{
    /// <summary>
    /// Controller interface for Twino MVC
    /// </summary>
    public interface IController
    {
        /// <summary>
        /// HTTP Request
        /// </summary>
        HttpRequest Request { get; }

        /// <summary>
        /// HTTP Response
        /// </summary>
        HttpResponse Response { get; }

        /// <summary>
        /// Underlying HTTP Server object of Twino MVC
        /// </summary>
        ITwinoServer Server { get; }

        /// <summary>
        /// Get Claims for user associated for executing request
        /// </summary>
        ClaimsPrincipal User { get; }
    }
}
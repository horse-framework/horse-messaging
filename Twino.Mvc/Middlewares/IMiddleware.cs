using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Mvc.Controllers;

namespace Twino.Mvc.Middlewares
{
    /// <summary>
    /// Middleware set result handler. If a middleware calls and passes non-null value as IActionResult arg,
    /// middleware chain stops and returns that result as HTTP response.
    /// </summary>
    public delegate void MiddlewareResultHandler(IActionResult result = null);

    /// <summary>
    /// MVC Middleware
    /// </summary>
    public interface IMiddleware
    {
        /// <summary>
        /// Middleware invoke method. For dependency inversion, use middleware constructor
        /// </summary>
        Task Invoke(HttpRequest request, HttpResponse response, MiddlewareResultHandler setResult);
    }
}

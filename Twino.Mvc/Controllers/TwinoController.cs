using Twino.Mvc.Results;
using Twino.Server;
using System.Security.Claims;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Twino.Mvc.Controllers
{
    /// <summary>
    /// Base controller object for Twino MVC
    /// Includes some useful methods.
    /// </summary>
    public class TwinoController : IController
    {
        /// <summary>
        /// Gets HTTP Request information for the specified request
        /// </summary>
        public HttpRequest Request { get; internal set; }

        /// <summary>
        /// Gets HTTP Response object for the request
        /// </summary>
        public HttpResponse Response { get; internal set; }

        /// <summary>
        /// Gets Underlying HTTP Server object
        /// </summary>
        public TwinoServer Server { get; internal set; }

        /// <summary>
        /// Get Claims for user associated for executing request
        /// </summary>
        public ClaimsPrincipal User { get; internal set; }

        /// <summary>
        /// Creates new JSON result from the object. Status code will be set to 200.
        /// </summary>
        protected IActionResult Json(object obj)
        {
            JsonResult json = new JsonResult();
            json.Set(obj);
            return json;
        }

        /// <summary>
        /// Creates new JSON result from the object. Status code will be set to 200.
        /// </summary>
        protected async Task<IActionResult> JsonAsync(object obj)
        {
            JsonResult json = new JsonResult();
            await json.SetAsync(obj);
            return json;
        }

        /// <summary>
        /// Creates new XML result from the object. Status code will be set to 200.
        /// </summary>
        protected IActionResult Xml(object obj)
        {
            return new XmlResult(obj);
        }

        /// <summary>
        /// Creates new XML result from the object. Status code will be set to 200.
        /// </summary>
        protected async Task<IActionResult> XmlAsync(object obj)
        {
            return await Task.FromResult(new XmlResult(obj));
        }

        /// <summary>
        /// Returns redirect result. URL should be absolute.
        /// </summary>
        protected IActionResult Redirect(string url, bool permanent)
        {
            return permanent ? StatusCodeResult.MovedPermanently(url) : StatusCodeResult.Redirect(url);
        }

        /// <summary>
        /// Returns redirect result. URL should be absolute.
        /// </summary>
        protected async Task<IActionResult> RedirectAsync(string url, bool permanent)
        {
            return await Task.FromResult(permanent ? StatusCodeResult.MovedPermanently(url) : StatusCodeResult.Redirect(url));
        }

        /// <summary>
        /// Creates new plain text result from the value. Status code will be set to 200.
        /// </summary>
        protected IActionResult String(string content)
        {
            return new StringResult(content);
        }

        /// <summary>
        /// Creates new plain text result from the value. Status code will be set to 200.
        /// </summary>
        protected async Task<IActionResult> StringAsync(string content)
        {
            return await Task.FromResult(new StringResult(content));
        }
    }
}
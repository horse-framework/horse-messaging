using System.IO;
using System.Net;
using Twino.Mvc.Results;
using Twino.Server;
using System.Security.Claims;
using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Ioc;

namespace Twino.Mvc.Controllers
{
    /// <summary>
    /// Base controller object for Twino MVC
    /// Includes some useful methods.
    /// </summary>
    public class TwinoController : IController
    {
        
        #region Properties
        
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
        /// Gets IOC scope of the request
        /// </summary>
        public IContainerScope CurrentScope { get; internal set; }

        #endregion
        
        #region Return Results
        
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
        protected async Task<IActionResult> JsonAsync(object obj, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            JsonResult json = new JsonResult(statusCode);
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

        /// <summary>
        /// Creates new file result
        /// </summary>
        protected async Task<IActionResult> File(Stream fileStream, string filename)
        {
            return await Task.FromResult(new FileResult(fileStream, filename));
        }

        #endregion
        
        #region Status Code - Json
        
        /// <summary>
        /// Returns JSON result with 200 HTTP status code
        /// </summary>
        protected async Task<IActionResult> Ok(object model)
        {
            return await JsonAsync(model);
        }
 
        /// <summary>
        /// Returns JSON result with Created HTTP status code
        /// </summary>
        protected async Task<IActionResult> Created(object model)
        {
            return await JsonAsync(model, HttpStatusCode.Created);
        }
        
        /// <summary>
        /// Returns JSON result with Accepted HTTP status code
        /// </summary>
        protected async Task<IActionResult> Accepted(object model)
        {
            return await JsonAsync(model, HttpStatusCode.Accepted);
        }
        
        /// <summary>
        /// Returns JSON result with BadRequest HTTP status code
        /// </summary>
        protected async Task<IActionResult> BadRequest(object model)
        {
            return await JsonAsync(model, HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Returns JSON result with BadGateway HTTP status code
        /// </summary>
        protected async Task<IActionResult> BadGateway(object model)
        {
            return await JsonAsync(model, HttpStatusCode.BadGateway);
        }

        /// <summary>
        /// Returns JSON result with Unauthorized HTTP status code
        /// </summary>
        protected async Task<IActionResult> Unauthorized(object model)
        {
            return await JsonAsync(model, HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Returns JSON result with PaymentRequired HTTP status code
        /// </summary>
        protected async Task<IActionResult> PaymentRequired(object model)
        {
            return await JsonAsync(model, HttpStatusCode.PaymentRequired);
        }
        
        /// <summary>
        /// Returns JSON result with Forbidden HTTP status code
        /// </summary>
        protected async Task<IActionResult> Forbidden(object model)
        {
            return await JsonAsync(model, HttpStatusCode.Forbidden);
        }
        
        /// <summary>
        /// Returns JSON result with NotFound HTTP status code
        /// </summary>
        protected async Task<IActionResult> NotFound(object model)
        {
            return await JsonAsync(model, HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Returns JSON result with MethodNotAllowed HTTP status code
        /// </summary>
        protected async Task<IActionResult> MethodNotAllowed(object model)
        {
            return await JsonAsync(model, HttpStatusCode.MethodNotAllowed);
        }
        
        /// <summary>
        /// Returns JSON result with NotAcceptable HTTP status code
        /// </summary>
        protected async Task<IActionResult> NotAcceptable(object model)
        {
            return await JsonAsync(model, HttpStatusCode.NotAcceptable);
        }
        
        /// <summary>
        /// Returns JSON result with Conflict HTTP status code
        /// </summary>
        protected async Task<IActionResult> Conflict(object model)
        {
            return await JsonAsync(model, HttpStatusCode.Conflict);
        }
        
        /// <summary>
        /// Returns JSON result with Gone HTTP status code
        /// </summary>
        protected async Task<IActionResult> Gone(object model)
        {
            return await JsonAsync(model, HttpStatusCode.Gone);
        }
        
        /// <summary>
        /// Returns JSON result with LengthRequired HTTP status code
        /// </summary>
        protected async Task<IActionResult> LengthRequired(object model)
        {
            return await JsonAsync(model, HttpStatusCode.LengthRequired);
        }
        
        /// <summary>
        /// Returns JSON result with PreconditionFailed HTTP status code
        /// </summary>
        protected async Task<IActionResult> PreconditionFailed(object model)
        {
            return await JsonAsync(model, HttpStatusCode.PreconditionFailed);
        }
        
        /// <summary>
        /// Returns JSON result with RequestEntityTooLarge HTTP status code
        /// </summary>
        protected async Task<IActionResult> RequestEntityTooLarge(object model)
        {
            return await JsonAsync(model, HttpStatusCode.RequestEntityTooLarge);
        }
        
        /// <summary>
        /// Returns JSON result with UnsupportedMediaType HTTP status code
        /// </summary>
        protected async Task<IActionResult> UnsupportedMediaType(object model)
        {
            return await JsonAsync(model, HttpStatusCode.UnsupportedMediaType);
        }

        /// <summary>
        /// Returns JSON result with ExpectationFailed HTTP status code
        /// </summary>
        protected async Task<IActionResult> ExpectationFailed(object model)
        {
            return await JsonAsync(model, HttpStatusCode.ExpectationFailed);
        }

        /// <summary>
        /// Returns JSON result with NotImplemented HTTP status code
        /// </summary>
        protected async Task<IActionResult> NotImplemented(object model)
        {
            return await JsonAsync(model, HttpStatusCode.NotImplemented);
        }

        #endregion
        
    }
}
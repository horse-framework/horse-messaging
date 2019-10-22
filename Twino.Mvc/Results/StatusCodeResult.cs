using Twino.Mvc.Controllers;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Http;

namespace Twino.Mvc.Results
{
    public class StatusCodeResult : IActionResult
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
        public string Content { get; private set; }

        /// <summary>
        /// Additional custom headers with key and value
        /// </summary>
        public Dictionary<string, string> Headers { get; private set; }

        public StatusCodeResult(HttpStatusCode code)
        {
            Headers = new Dictionary<string, string>();
            ContentType = ContentTypes.TEXT_HTML;
            Content = null;
            Code = code;
        }

        /// <summary>
        /// 200 - OK
        /// </summary>
        public static IActionResult Ok()
        {
            return new StatusCodeResult(HttpStatusCode.OK);
        }

        /// <summary>
        /// 200 - OK
        /// </summary>
        public static async Task<IActionResult> OkAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.OK));
        }

        /// <summary>
        /// 201 - Created
        /// </summary>
        public static IActionResult Created()
        {
            return new StatusCodeResult(HttpStatusCode.Created);
        }

        /// <summary>
        /// 201 - Created
        /// </summary>
        public static async Task<IActionResult> CreatedAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.Created));
        }

        /// <summary>
        /// 202 - Accepted
        /// </summary>
        public static IActionResult Accepted()
        {
            return new StatusCodeResult(HttpStatusCode.Accepted);
        }

        /// <summary>
        /// 202 - Accepted
        /// </summary>
        public static async Task<IActionResult> AcceptedAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.Accepted));
        }

        /// <summary>
        /// 203 - Non Authoritative Information
        /// </summary>
        public static IActionResult NonAuthoritativeInformation()
        {
            return new StatusCodeResult(HttpStatusCode.NonAuthoritativeInformation);
        }

        /// <summary>
        /// 203 - Non Authoritative Information
        /// </summary>
        public static async Task<IActionResult> NonAuthoritativeInformationAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.NonAuthoritativeInformation));
        }

        /// <summary>
        /// 204 - No Content
        /// </summary>
        public static IActionResult NoContent()
        {
            return new StatusCodeResult(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// 204 - No Content
        /// </summary>
        public static async Task<IActionResult> NoContentAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.NoContent));
        }

        /// <summary>
        /// 205 - Reset Content
        /// </summary>
        public static IActionResult ResetContent()
        {
            return new StatusCodeResult(HttpStatusCode.ResetContent);
        }

        /// <summary>
        /// 205 - Reset Content
        /// </summary>
        public static async Task<IActionResult> ResetContentAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.ResetContent));
        }

        /// <summary>
        /// 301 - Moved Permanently
        /// </summary>
        public static IActionResult MovedPermanently(string location)
        {
            StatusCodeResult result = new StatusCodeResult(HttpStatusCode.MovedPermanently);
            result.Headers.Add(HttpHeaders.LOCATION, location);
            result.Content = "<html><head><title>Moved</title></head><body><div>Page moved to <a href=\"" + location + "\">here</a></div></body></html>";
            return result;
        }

        /// <summary>
        /// 301 - Moved Permanently
        /// </summary>
        public static async Task<IActionResult> MovedPermanentlyAsync(string location)
        {
            return await Task.FromResult(MovedPermanently(location));
        }

        /// <summary>
        /// 302 - Found
        /// </summary>
        public static IActionResult Found(string location)
        {
            StatusCodeResult result = new StatusCodeResult(HttpStatusCode.Found);
            result.Headers.Add(HttpHeaders.LOCATION, location);
            result.Content = "<html><head><title>Moved</title></head><body><div>Page moved to <a href=\"" + location + "\">here</a></div></body></html>";
            return result;
        }

        /// <summary>
        /// 302 - Found
        /// </summary>
        public static async Task<IActionResult> FoundAsync(string location)
        {
            return await Task.FromResult(Found(location));
        }

        /// <summary>
        /// 302 - Redirect
        /// </summary>
        public static IActionResult Redirect(string location)
        {
            StatusCodeResult result = new StatusCodeResult(HttpStatusCode.Redirect);
            result.Headers.Add(HttpHeaders.LOCATION, location);
            result.Content = "<html><head><title>Moved</title></head><body><div>Page moved to <a href=\"" + location + "\">here</a></div></body></html>";
            return result;
        }

        /// <summary>
        /// 302 - Redirect
        /// </summary>
        public static async Task<IActionResult> RedirectAsync(string location)
        {
            return await Task.FromResult(Redirect(location));
        }

        /// <summary>
        /// 307 - Temporary Redirect
        /// </summary>
        public static IActionResult TemporaryRedirect(string location)
        {
            StatusCodeResult result = new StatusCodeResult(HttpStatusCode.TemporaryRedirect);
            result.Headers.Add(HttpHeaders.LOCATION, location);
            result.Content = "<html><head><title>Moved</title></head><body><div>Page moved to <a href=\"" + location + "\">here</a></div></body></html>";
            return result;
        }

        /// <summary>
        /// 307 - Temporary Redirect
        /// </summary>
        public static async Task<IActionResult> TemporaryRedirectAsync(string location)
        {
            return await Task.FromResult(TemporaryRedirect(location));
        }

        /// <summary>
        /// 308 - Permanent Redirect
        /// </summary>
        public static IActionResult PermanentRedirect(string location)
        {
            StatusCodeResult result = new StatusCodeResult(HttpStatusCode.PermanentRedirect);
            result.Headers.Add(HttpHeaders.LOCATION, location);
            result.Content = "<html><head><title>Moved</title></head><body><div>Page moved to <a href=\"" + location + "\">here</a></div></body></html>";
            return result;
        }

        /// <summary>
        /// 308 - Permanent Redirect
        /// </summary>
        public static async Task<IActionResult> PermanentRedirectAsync(string location)
        {
            return await Task.FromResult(PermanentRedirect(location));
        }

        /// <summary>
        /// 400 - Bad Request
        /// </summary>
        public static IActionResult BadRequest()
        {
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// 400 - Bad Request
        /// </summary>
        public static async Task<IActionResult> BadRequestAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.BadRequest));
        }

        /// <summary>
        /// 401 - Unauthorized
        /// </summary>
        public static IActionResult Unauthorized()
        {
            return new StatusCodeResult(HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// 401 - Unauthorized
        /// </summary>
        public static async Task<IActionResult> UnauthorizedAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.Unauthorized));
        }

        /// <summary>
        /// 403 - Forbidden
        /// </summary>
        public static IActionResult Forbidden()
        {
            return new StatusCodeResult(HttpStatusCode.Forbidden);
        }

        /// <summary>
        /// 403 - Forbidden
        /// </summary>
        public static async Task<IActionResult> ForbiddenAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.Forbidden));
        }

        /// <summary>
        /// 404 - Not Found
        /// </summary>
        public static IActionResult NotFound()
        {
            return new StatusCodeResult(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// 404 - Not Found
        /// </summary>
        public static async Task<IActionResult> NotFoundAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.NotFound));
        }

        /// <summary>
        /// 405 - Method Not Allowed
        /// </summary>
        public static IActionResult MethodNotAllowed()
        {
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        /// <summary>
        /// 405 - Method Not Allowed
        /// </summary>
        public static async Task<IActionResult> MethodNotAllowedAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.MethodNotAllowed));
        }

        /// <summary>
        /// 406 - Not Acceptable
        /// </summary>
        public static IActionResult NotAcceptable()
        {
            return new StatusCodeResult(HttpStatusCode.NotAcceptable);
        }

        /// <summary>
        /// 406 - Not Acceptable
        /// </summary>
        public static async Task<IActionResult> NotAcceptableAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.NotAcceptable));
        }

        /// <summary>
        /// 408 - Request Timeout
        /// </summary>
        public static IActionResult RequestTimeout()
        {
            return new StatusCodeResult(HttpStatusCode.RequestTimeout);
        }

        /// <summary>
        /// 408 - Request Timeout
        /// </summary>
        public static async Task<IActionResult> RequestTimeoutAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.RequestTimeout));
        }

        /// <summary>
        /// 409 - Conflict
        /// </summary>
        public static IActionResult Conflict()
        {
            return new StatusCodeResult(HttpStatusCode.Conflict);
        }

        /// <summary>
        /// 409 - Conflict
        /// </summary>
        public static async Task<IActionResult> ConflictAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.Conflict));
        }

        /// <summary>
        /// 410 - Gone
        /// </summary>
        public static IActionResult Gone()
        {
            return new StatusCodeResult(HttpStatusCode.Gone);
        }

        /// <summary>
        /// 410 - Gone
        /// </summary>
        public static async Task<IActionResult> GoneAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.Gone));
        }

        /// <summary>
        /// 411 - Length Required
        /// </summary>
        public static IActionResult LengthRequired()
        {
            return new StatusCodeResult(HttpStatusCode.LengthRequired);
        }

        /// <summary>
        /// 411 - Length Required
        /// </summary>
        public static async Task<IActionResult> LengthRequiredAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.LengthRequired));
        }

        /// <summary>
        /// 413 - Request Entity Too Large
        /// </summary>
        public static IActionResult RequestEntityTooLarge()
        {
            return new StatusCodeResult(HttpStatusCode.RequestEntityTooLarge);
        }

        /// <summary>
        /// 413 - Request Entity Too Large
        /// </summary>
        public static async Task<IActionResult> RequestEntityTooLargeAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.RequestEntityTooLarge));
        }

        /// <summary>
        /// 201 - Created
        /// </summary>
        public static IActionResult RequestUriTooLong()
        {
            return new StatusCodeResult(HttpStatusCode.RequestUriTooLong);
        }

        /// <summary>
        /// 201 - Created
        /// </summary>
        public static async Task<IActionResult> RequestUriTooLongAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.RequestUriTooLong));
        }

        /// <summary>
        /// 415 - Unsupported Media Type
        /// </summary>
        public static IActionResult UnsupportedMediaType()
        {
            return new StatusCodeResult(HttpStatusCode.UnsupportedMediaType);
        }

        /// <summary>
        /// 415 - Unsupported Media Type
        /// </summary>
        public static async Task<IActionResult> UnsupportedMediaTypeAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.UnsupportedMediaType));
        }

        /// <summary>
        /// 417 - Expectation Failed
        /// </summary>
        public static IActionResult ExpectationFailed()
        {
            return new StatusCodeResult(HttpStatusCode.ExpectationFailed);
        }

        /// <summary>
        /// 417 - Expectation Failed
        /// </summary>
        public static async Task<IActionResult> ExpectationFailedAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.ExpectationFailed));
        }

        /// <summary>
        /// 423 - Locked
        /// </summary>
        public static IActionResult Locked()
        {
            return new StatusCodeResult(HttpStatusCode.Locked);
        }

        /// <summary>
        /// 423 - Locked
        /// </summary>
        public static async Task<IActionResult> LockedAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.Locked));
        }

        /// <summary>
        /// 428 - Precondition Required
        /// </summary>
        public static IActionResult PreconditionRequired()
        {
            return new StatusCodeResult(HttpStatusCode.PreconditionRequired);
        }

        /// <summary>
        /// 428 - Precondition Required
        /// </summary>
        public static async Task<IActionResult> PreconditionRequiredAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.PreconditionRequired));
        }

        /// <summary>
        /// 429 - Too Many Requests
        /// </summary>
        public static IActionResult TooManyRequests()
        {
            return new StatusCodeResult(HttpStatusCode.TooManyRequests);
        }

        /// <summary>
        /// 429 - Too Many Requests
        /// </summary>
        public static async Task<IActionResult> TooManyRequestsAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.TooManyRequests));
        }

        /// <summary>
        /// 431 - Request Header Fields Too Large
        /// </summary>
        public static IActionResult RequestHeaderFieldsTooLarge()
        {
            return new StatusCodeResult(HttpStatusCode.RequestHeaderFieldsTooLarge);
        }

        /// <summary>
        /// 431 - Request Header Fields Too Large
        /// </summary>
        public static async Task<IActionResult> RequestHeaderFieldsTooLargeAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.RequestHeaderFieldsTooLarge));
        }

        /// <summary>
        /// 500 - Internal Server Error
        /// </summary>
        public static IActionResult InternalServerError()
        {
            return new StatusCodeResult(HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// 500 - Internal Server Error
        /// </summary>
        public static async Task<IActionResult> InternalServerErrorAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.InternalServerError));
        }

        /// <summary>
        /// 501 - Not Implemented
        /// </summary>
        public static IActionResult NotImplemented()
        {
            return new StatusCodeResult(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// 501 - Not Implemented
        /// </summary>
        public static async Task<IActionResult> NotImplementedAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.NotImplemented));
        }

        /// <summary>
        /// 502 - Bad Gateway
        /// </summary>
        public static IActionResult BadGateway()
        {
            return new StatusCodeResult(HttpStatusCode.BadGateway);
        }

        /// <summary>
        /// 502 - Bad Gateway
        /// </summary>
        public static async Task<IActionResult> BadGatewayAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.BadGateway));
        }

        /// <summary>
        /// 503 - Service Unavailable
        /// </summary>
        public static IActionResult ServiceUnavailable()
        {
            return new StatusCodeResult(HttpStatusCode.ServiceUnavailable);
        }
        
        /// <summary>
        /// 503 - Service Unavailable
        /// </summary>
        public static async Task<IActionResult> ServiceUnavailableAsync()
        {
            return await Task.FromResult(new StatusCodeResult(HttpStatusCode.ServiceUnavailable));
        }
    }
}
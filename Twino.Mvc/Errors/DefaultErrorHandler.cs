using System;
using System.Net;
using Twino.Core.Http;
using Twino.Mvc.Controllers;
using Twino.Mvc.Results;
using Twino.Server.Http;

namespace Twino.Mvc.Errors
{
    /// <summary>
    /// Default Error Handler.
    /// If there is no error handler in the project
    /// to disable development error handler in production mode
    /// this handler will be activated
    /// </summary>
    public class DefaultErrorHandler : IErrorHandler
    {
        /// <summary>
        /// Writes a short 500 - Internal Server Error to the response.
        /// Hides exception information
        /// </summary>
        public void Error(HttpRequest request, Exception ex)
        {
            HtmlResult error = new HtmlResult(PredefinedResults.Statuses[HttpStatusCode.InternalServerError]);
            WriteResponse(request.Response, error);
        }

        /// <summary>
        /// Writes IActionResult to the response
        /// </summary>
        private void WriteResponse(HttpResponse response, IActionResult result)
        {
            response.StatusCode = result.Code;
            response.ContentType = result.ContentType;
            response.AdditionalHeaders = response.AdditionalHeaders;
            response.Write(result.Stream);
        }
    }
}

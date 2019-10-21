using System;
using System.Text;
using Twino.Core.Http;
using Twino.Mvc.Controllers;
using Twino.Mvc.Results;

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
            StringBuilder builder = new StringBuilder();
            builder.Append("<html>");
            builder.Append("<head><title>500 - Internal Server Error</title></head>");
            builder.Append("<body style=\"font-family: Arial; padding:40px;\">");
            builder.Append("<div style=\"text-align:center; padding:60px; font-size:32px; font-weight:400; color:#666;;\">");
            builder.Append("Internal Server Error");
            builder.Append("<span style=\"display:block; padding:20px; text-align:center; font-size:12px; color:#999;\">Twino Server</span>");
            builder.Append("</div>");
            builder.Append("</body>");
            builder.Append("</html>");

            HtmlResult error = new HtmlResult(builder.ToString().Replace("\n", "<br>"));
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

            if (!string.IsNullOrEmpty(result.Content))
                response.Write(result.Content);
        }
    }
}

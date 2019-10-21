using System;
using System.Text;
using Twino.Core.Http;
using Twino.Mvc.Controllers;
using Twino.Mvc.Results;

namespace Twino.Mvc.Errors
{
    /// <summary>
    /// Development mode error handler.
    /// This class writes all exception information to the output.
    /// DO NOT USE IN PRODUCTION
    /// </summary>
    public class DevelopmentErrorHandler : IErrorHandler
    {

        /// <summary>
        /// Writes all exception information to the response
        /// </summary>
        public void Error(HttpRequest request, Exception ex)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<html>");
            builder.Append("<head><title>500 - Internal Server Error</title></head>");
            builder.Append("<body style=\"font-family: Arial; padding:40px;\">");
            builder.Append("<div style=\"border:1px solid #ccc;\">");
            builder.Append("<h1 style=\"margin:0px; padding:20px; display:block; background-color:#f0f0f0; color:#222; border-bottom:1px solid #ccc;\">Internal Server Error (500)</h1>");
            builder.Append("<div style=\"padding:20px;\">");
            builder.Append("<h3 style=\"border-bottom:1px solid #dadada; display:block; padding:5px 0px 25px 0px; margin-bottom:20px;\">" + ex.Message + "</h3>");
            builder.Append("<h5>Source</h5>");
            builder.Append("<p>" + ex.Source + "</p>");
            builder.Append("<h5>Stack Trace</h5>");
            builder.Append("<p style=\"line-height:28px; font-size:14px; font-weight:400; color:#555\">" + ex.StackTrace + "</p>");
            builder.Append("<p style=\"font-size:12px; text-align:center; margin-top:20px; border-top:1px solid #dadada; padding:25px 10px 0px 10px; font-weight:400; color:#666\">Twino MVC Server</p>");
            builder.Append("</div>");
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

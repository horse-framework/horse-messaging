using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Mvc.Controllers;
using Twino.Mvc.Middlewares;
using Twino.Mvc.Results;

namespace Test.Mvc.Helpers
{
    public class CustomMiddleware : IMiddleware
    {
        public async Task Invoke(NextMiddlewareHandler next, HttpRequest request, HttpResponse response)
        {
            Dictionary<string, string> queryStrings = request.GetQueryStringValues();

            if (queryStrings.ContainsKey("middleware"))
            {
                string value = queryStrings["middleware"];
                if (value == "custom")
                {
                    IActionResult result = new StatusCodeResult(HttpStatusCode.Forbidden);
                    next(result);
                    return;
                }
            }

            next(null);
            await Task.CompletedTask;
        }
    }
}
using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Mvc.Results;

namespace Twino.Mvc.Middlewares
{
    public class CorsMiddleware : IMiddleware
    {
        public string AllowedOrigins { get; set; }
        public string AllowedMethods { get; set; }
        public string AllowedHeaders { get; set; }
        public string AllowMaxAge { get; set; }
        public bool AllowCredentials { get; set; }

        public void AllowAll()
        {
            AllowedOrigins = "*";
            AllowedMethods = "OPTIONS,POST,GET,PUT,DELETE";
            AllowedHeaders = "access-control-allow-origin,content-type,content-length,authorization";
            AllowMaxAge = "3600";
            AllowCredentials = true;
        }

        public async Task Invoke(NextMiddlewareHandler next, HttpRequest request, HttpResponse response)
        {
            if (AllowCredentials)
                response.AdditionalHeaders.Add("Access-Control-Allow-Credentials", "true");

            if (!string.IsNullOrEmpty(AllowedOrigins))
            {
                if (AllowedOrigins == "*")
                {
                    if (request.Headers.ContainsKey(HttpHeaders.ORIGIN))
                        response.AdditionalHeaders.Add("Access-Control-Allow-Origin", request.Headers[HttpHeaders.ORIGIN]);
                    else
                        response.AdditionalHeaders.Add("Access-Control-Allow-Origin", "*");

                }
                else
                    response.AdditionalHeaders.Add("Access-Control-Allow-Origin", AllowedOrigins);
            }

            if (request.Method == "OPTIONS")
            {
                if (!string.IsNullOrEmpty(AllowedHeaders))
                    response.AdditionalHeaders.Add("Access-Control-Allow-Headers", AllowedHeaders);

                if (!string.IsNullOrEmpty(AllowedMethods))
                    response.AdditionalHeaders.Add("Access-control-Allow-Methods", AllowedMethods);

                if (!string.IsNullOrEmpty(AllowMaxAge))
                    response.AdditionalHeaders.Add("Access-Control-Max-Age", AllowMaxAge);

                next(StatusCodeResult.NoContent());
                return;
            }

            next(null);
            await Task.CompletedTask;
        }
    }
}

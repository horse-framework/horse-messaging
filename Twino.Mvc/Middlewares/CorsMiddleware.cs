using System;
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
                response.AdditionalHeaders.Add(HttpHeaders.ACCESS_CONTROL_ALLOW_CREDENTIALS, "true");

            if (!string.IsNullOrEmpty(AllowedOrigins))
            {
                if (AllowedOrigins == "*")
                {
                    response.AdditionalHeaders.Add(HttpHeaders.ACCESS_CONTROL_ALLOW_ORIGIN,
                                                   request.Headers.ContainsKey(HttpHeaders.ORIGIN)
                                                       ? request.Headers[HttpHeaders.ORIGIN]
                                                       : "*");
                }
                else
                    response.AdditionalHeaders.Add(HttpHeaders.ACCESS_CONTROL_ALLOW_ORIGIN, AllowedOrigins);
            }

            if (request.Method.Equals(HttpHeaders.HTTP_OPTIONS, StringComparison.InvariantCultureIgnoreCase))
            {
                if (!string.IsNullOrEmpty(AllowedHeaders))
                    response.AdditionalHeaders.Add(HttpHeaders.ACCESS_CONTROL_ALLOW_HEADERS, AllowedHeaders);

                if (!string.IsNullOrEmpty(AllowedMethods))
                    response.AdditionalHeaders.Add(HttpHeaders.ACCESS_CONTROL_ALLOW_METHODS, AllowedMethods);

                if (!string.IsNullOrEmpty(AllowMaxAge))
                    response.AdditionalHeaders.Add(HttpHeaders.ACCESS_CONTROL_MAX_AGE, AllowMaxAge);

                next(StatusCodeResult.NoContent());
                return;
            }

            next(null);
            await Task.CompletedTask;
        }
    }
}
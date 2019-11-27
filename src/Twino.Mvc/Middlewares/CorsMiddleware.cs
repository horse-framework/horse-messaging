using System;
using System.Threading.Tasks;
using Twino.Mvc.Results;
using Twino.Protocols.Http;

namespace Twino.Mvc.Middlewares
{
    /// <summary>
    /// CORS (Cross Origin Request Sharing) middleware
    /// </summary>
    public class CorsMiddleware : IMiddleware
    {
        /// <summary>
        /// "*" for all origins. Otherwise type with comma seperator
        /// </summary>
        public string AllowedOrigins { get; set; }
        
        /// <summary>
        /// Allowed such as GET, POST, PUT
        /// </summary>
        public string AllowedMethods { get; set; }
        
        /// <summary>
        /// Allowed header keys
        /// </summary>
        public string AllowedHeaders { get; set; }
        
        /// <summary>
        /// Allowed max-age in seconds. Default is 3600.
        /// </summary>
        public string AllowMaxAge { get; set; }
        
        /// <summary>
        /// True, if need to allow credentials
        /// </summary>
        public bool AllowCredentials { get; set; }

        public void AllowAll()
        {
            AllowedOrigins = "*";
            AllowedMethods = "OPTIONS,POST,GET,PUT,DELETE";
            AllowedHeaders = "access-control-allow-origin,content-type,content-length,authorization";
            AllowMaxAge = "3600";
            AllowCredentials = true;
        }

        public async Task Invoke(HttpRequest request, HttpResponse response, MiddlewareResultHandler setResult)
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

                setResult(StatusCodeResult.NoContent());
            }

            await Task.CompletedTask;
        }
    }
}
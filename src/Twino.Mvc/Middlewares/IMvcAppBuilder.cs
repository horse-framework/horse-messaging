using System;
using System.Net;
using Twino.Protocols.Http;

namespace Twino.Mvc.Middlewares
{
    /// <summary>
    /// Application builder for twino MVC
    /// </summary>
    public interface IMvcAppBuilder
    {
        /// <summary>
        /// Uses middleware from instance
        /// </summary>
        void UseMiddleware(IMiddleware middleware);

        /// <summary>
        /// Creates new instance of type and uses it as middleware
        /// </summary>
        void UseMiddleware<TMiddleware>() where TMiddleware : IMiddleware;

        /// <summary>
        /// Uses static files in specific physical path
        /// </summary>
        void UseFiles(string urlPath, string physicalPath);

        /// <summary>
        /// Uses static files in multiple physical paths.
        /// Searches requested files in index order.
        /// </summary>
        void UseFiles(string urlPath, string[] physicalPaths);

        /// <summary>
        /// Uses static files in specific physical path.
        /// Each request is filtered by validation method
        /// </summary>
        void UseFiles(string urlPath, string physicalPath, Func<HttpRequest, HttpStatusCode> validation);

        /// <summary>
        /// Uses static files in multiple physical paths.
        /// Searches requested files in index order.
        /// Each request is filtered by validation method
        /// </summary>
        void UseFiles(string urlPath, string[] physicalPaths, Func<HttpRequest, HttpStatusCode> validation);
    }
}
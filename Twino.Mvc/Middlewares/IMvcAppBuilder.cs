using System;
using System.Net;
using Twino.Protocols.Http;

namespace Twino.Mvc.Middlewares
{
    public interface IMvcAppBuilder
    {
        void UseMiddleware(IMiddleware middleware);
        void UseMiddleware<TMiddleware>() where TMiddleware : IMiddleware;

        void UseFiles(string urlPath, string physicalPath);
        void UseFiles(string urlPath, string[] physicalPaths);
        void UseFiles(string urlPath, string physicalPath, Func<HttpRequest, HttpStatusCode> validation);
        void UseFiles(string urlPath, string[] physicalPaths, Func<HttpRequest, HttpStatusCode> validation);
    }
}

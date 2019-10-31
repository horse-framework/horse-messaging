using Twino.Mvc.Controllers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Twino.Mvc.Middlewares
{
    internal class MvcApp : IMvcApp
    {
        internal List<IMiddleware> Middlewares { get; set; }

        internal NextMiddlewareHandler Next { get; set; }

        public HttpRequest Request { get; set; }
        public HttpResponse Response { get; set; }

        public bool CanNext { get; private set; }

        public IActionResult LastResult { get; private set; }

        public MvcApp(HttpRequest request, HttpResponse response)
        {
            Middlewares = new List<IMiddleware>();
            Request = request;
            Response = response;
            CanNext = true;
            Next = async result =>
            {
                LastResult = result;
                CanNext = result == null;
                await Task.CompletedTask;
            };
        }

        public void UseMiddleware(IMiddleware middleware)
        {
            Middlewares.Add(middleware);
        }

        public async Task RunSequence()
        {
            foreach (IMiddleware middleware in Middlewares)
            {
                CanNext = false;
                await middleware.Invoke(Next, Request, Response);
                if (!CanNext)
                    break;
            }
        }
    }
}
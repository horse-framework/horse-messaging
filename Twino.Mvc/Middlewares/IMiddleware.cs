using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Mvc.Controllers;

namespace Twino.Mvc.Middlewares
{
    public delegate void NextMiddlewareHandler(IActionResult result);

    public interface IMiddleware
    {
        Task Invoke(NextMiddlewareHandler next, HttpRequest request, HttpResponse response);
    }
}

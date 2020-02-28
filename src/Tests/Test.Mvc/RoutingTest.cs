using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Routing;
using Twino.Protocols.Http;
using Xunit;

namespace Test.Mvc
{
    public class RoutingTest
    {
        /*
         * controller: plain, [ name ], { parameter }, multiple bindings, nested /
         * 
         */

        [Theory]
        [InlineData("GET", "/")]
        public async Task Test(string method, string path)
        {
            TwinoMvc mvc = new TwinoMvc();
            mvc.Init();
            mvc.CreateRoutes(Assembly.GetExecutingAssembly());

            HttpRequest request = new HttpRequest();
            request.Method = method;
            request.Path = path;

            HttpResponse response = new HttpResponse();

            RouteMatch match = mvc.RouteFinder.Find(mvc.Routes, request);
            Assert.NotNull(match);

            TwinoController controller = await mvc.ControllerFactory.CreateInstance(mvc, match.Route.ControllerType, request, response, mvc.Services.CreateScope());

            var parameters = MvcConnectionHandler.FillParameters(request, match).Select(x => x.Value).ToArray();
            Task<IActionResult> task = (Task<IActionResult>) match.Route.ActionType.Invoke(controller, parameters);

            IActionResult result = task.Result;
            string url = Encoding.UTF8.GetString(((MemoryStream) result.Stream).ToArray());
            Assert.Equal(url, path);
        }
    }
}
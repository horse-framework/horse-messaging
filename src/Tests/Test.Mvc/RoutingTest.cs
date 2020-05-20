using FluentAssertions;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Test.Mvc.Arrange;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Routing;
using Twino.Protocols.Http;
using Xunit;

namespace Test.Mvc
{
    public class RoutingTest
    {
        [Theory]
        [ClassData(typeof(RoutingTestData))]
        public async Task FindRoutes(string method, string path, string aResult)
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

            var parameters = (await MvcConnectionHandler.FillParameters(request, match)).Select(x => x.Value).ToArray();
            Task<IActionResult> task = (Task<IActionResult>) match.Route.ActionType.Invoke(controller, parameters);

            IActionResult result = task.Result;
            string url = Encoding.UTF8.GetString(((MemoryStream) result.Stream).ToArray());
            url.Should().Be(aResult);
        }
    }
}
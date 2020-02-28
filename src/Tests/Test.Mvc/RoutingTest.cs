using FluentAssertions;
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
        [Theory]
        [InlineData("GET", "/", "get/")]
        [InlineData("POST", "/", "post/")]
        [InlineData("GET", "/a", "geta/")]
        [InlineData("POST", "/a", "posta/")]
        [InlineData("GET", "/b/3", "getb/3")]
        [InlineData("POST", "/b/3", "postb/3")]
        [InlineData("GET", "/b", "getb/")]
        [InlineData("POST", "/b", "postb/")]
        [InlineData("GET", "/c/4", "getc/4")]
        [InlineData("POST", "/c/4", "postc/4")]
        [InlineData("GET", "/d/5", "getd/5/")]
        [InlineData("POST", "/d/5", "postd/5/")]
        [InlineData("GET", "/e/6/7", "gete/6/7")]
        [InlineData("POST", "/e/6/7", "poste/6/7")]
        [InlineData("GET", "/getaa", "geta/")]
        [InlineData("POST", "/postaa", "posta/")]
        [InlineData("GET", "/getbb/3", "getb/3")]
        [InlineData("POST", "/postbb/3", "postb/3")]
        [InlineData("GET", "/getbb", "getb/")]
        [InlineData("POST", "/postbb", "postb/")]
        [InlineData("GET", "/getcc/4", "getc/4")]
        [InlineData("POST", "/postcc/4", "postc/4")]
        [InlineData("GET", "/getdd/5", "getd/5/")]
        [InlineData("POST", "/postdd/5", "postd/5/")]
        [InlineData("GET", "/getee/6/7", "gete/6/7")]
        [InlineData("POST", "/postee/6/7", "poste/6/7")]
        [InlineData("GET", "/test", "get/")]
        [InlineData("POST", "/test", "post/")]
        [InlineData("GET", "/test/a", "geta/")]
        [InlineData("POST", "/test/a", "posta/")]
        [InlineData("GET", "/test/b/3", "getb/3")]
        [InlineData("POST", "/test/b/3", "postb/3")]
        [InlineData("GET", "/test/b", "getb/")]
        [InlineData("POST", "/test/b", "postb/")]
        [InlineData("GET", "/test/c/4", "getc/4")]
        [InlineData("POST", "/test/c/4", "postc/4")]
        [InlineData("GET", "/test/d/5", "getd/5/")]
        [InlineData("POST", "/test/d/5", "postd/5/")]
        [InlineData("GET", "/test/e/6/7", "gete/6/7")]
        [InlineData("POST", "/test/e/6/7", "poste/6/7")]
        [InlineData("GET", "/test/getaa", "geta/")]
        [InlineData("POST", "/test/postaa", "posta/")]
        [InlineData("GET", "/test/getbb/3", "getb/3")]
        [InlineData("POST", "/test/postbb/3", "postb/3")]
        [InlineData("GET", "/test/getbb", "getb/")]
        [InlineData("POST", "/test/postbb", "postb/")]
        [InlineData("GET", "/test/getcc/4", "getc/4")]
        [InlineData("POST", "/test/postcc/4", "postc/4")]
        [InlineData("GET", "/test/getdd/5", "getd/5/")]
        [InlineData("POST", "/test/postdd/5", "postd/5/")]
        [InlineData("GET", "/test/getee/6/7", "gete/6/7")]
        [InlineData("POST", "/test/postee/6/7", "poste/6/7")]
        [InlineData("GET", "/test1", "get/")]
        [InlineData("POST", "/test1", "post/")]
        [InlineData("GET", "/test1/a", "geta/")]
        [InlineData("POST", "/test1/a", "posta/")]
        [InlineData("GET", "/test1/b/3", "getb/3")]
        [InlineData("POST", "/test1/b/3", "postb/3")]
        [InlineData("GET", "/test1/b", "getb/")]
        [InlineData("POST", "/test1/b", "postb/")]
        [InlineData("GET", "/test1/c/4", "getc/4")]
        [InlineData("POST", "/test1/c/4", "postc/4")]
        [InlineData("GET", "/test1/d/5", "getd/5/")]
        [InlineData("POST", "/test1/d/5", "postd/5/")]
        [InlineData("GET", "/test1/e/6/7", "gete/6/7")]
        [InlineData("POST", "/test1/e/6/7", "poste/6/7")]
        [InlineData("GET", "/test1/getaa", "geta/")]
        [InlineData("POST", "/test1/postaa", "posta/")]
        [InlineData("GET", "/test1/getbb/3", "getb/3")]
        [InlineData("POST", "/test1/postbb/3", "postb/3")]
        [InlineData("GET", "/test1/getbb", "getb/")]
        [InlineData("POST", "/test1/postbb", "postb/")]
        [InlineData("GET", "/test1/getcc/4", "getc/4")]
        [InlineData("POST", "/test1/postcc/4", "postc/4")]
        [InlineData("GET", "/test1/getdd/5", "getd/5/")]
        [InlineData("POST", "/test1/postdd/5", "postd/5/")]
        [InlineData("GET", "/test1/getee/6/7", "gete/6/7")]
        [InlineData("POST", "/test1/postee/6/7", "poste/6/7")]
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

            var parameters = MvcConnectionHandler.FillParameters(request, match).Select(x => x.Value).ToArray();
            Task<IActionResult> task = (Task<IActionResult>)match.Route.ActionType.Invoke(controller, parameters);

            IActionResult result = task.Result;
            string url = Encoding.UTF8.GetString(((MemoryStream)result.Stream).ToArray());
            url.Should().Be(aResult);
        }
    }
}
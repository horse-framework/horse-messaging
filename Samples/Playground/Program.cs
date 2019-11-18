using System.IO;
using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;
using Twino.Protocols.Http;
using Twino.Server;

namespace Playground
{
    [Route("")]
    public class HomeController : TwinoController
    {
        [HttpGet("")]
        public async Task<IActionResult> Get()
        {
            return await StringAsync("Hello, world!");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            HttpOptions options = new HttpOptions();
            TwinoMvc mvc = new TwinoMvc();
            mvc.Init(m => { });

            TwinoServer server = new TwinoServer(ServerOptions.CreateDefault());
            server.UseMvc(mvc, options);

            server.Start(82);
        }
    }
}
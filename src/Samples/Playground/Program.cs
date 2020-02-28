using System;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Data;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Controllers.Parameters;
using Twino.Mvc.Filters.Route;
using Twino.Protocols.TMQ;
using Twino.Server;

namespace Playground
{
    public class Login
    {
        public string User { get; set; }
        public string Pass { get; set; }
    }

    [Route("a")]
    public class AController : TwinoController
    {
        [HttpPost("b")]
        public async Task<IActionResult> B([FromForm] Login login)
        {
            return await StringAsync($"hello: {login.User} and {login.Pass}");
        }

        [HttpGet("c")]
        public async Task<IActionResult> B()
        {
            return await StringAsync("hello");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            TwinoMvc mvc = new TwinoMvc();
            mvc.Init();
            TwinoServer server = new TwinoServer();
            server.UseMvc(mvc);
            server.Start(26222);
            server.BlockWhileRunning();
        }
    }
}
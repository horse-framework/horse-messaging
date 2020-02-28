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

    public class User
    {
        public string Name { get; set; }
        public string Lastname { get; set; }
    }

    [Route("a")]
    public class AController : TwinoController
    {
        [HttpPost("b")]
        public async Task<IActionResult> B([FromForm("logi.n")] Login login1, [FromForm("use.r")] User user1, [FromForm("ya.s")]int yas)
        {
            return await StringAsync($"hello: {login1.User} and {login1.Pass} {user1.Name} - {user1.Lastname}");
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
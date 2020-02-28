using System;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Data;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;
using Twino.Protocols.TMQ;
using Twino.Server;

namespace Playground
{
   [Route("auth")]
    public class AuthController : TwinoController
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login(string user)
        {
            return await StringAsync("ok");
        }
    }


    class Program
    {
        static async Task Main(string[] args)
        {
            TwinoMvc mvc = new TwinoMvc();
            mvc.Init();

            TwinoServer server = new TwinoServer();
            server.UseMvc(mvc);
            server.Start(441);
            await server.BlockWhileRunningAsync();
        }
    }
}
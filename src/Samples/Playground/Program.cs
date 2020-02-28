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

    [Route("x")]
    public class BController : TwinoController
    {
        [HttpGet("c/{?x}")]
        public async Task<IActionResult> C(string x)
        {
            return await StringAsync("Xhello: " + x);
        }

        [HttpGet("")]
        public async Task<IActionResult> B()
        {
            return await StringAsync("Xhello");
        }

        [HttpGet("d")]
        public async Task<IActionResult> D()
        {
            return await StringAsync("Xhello D");
        }
    }


    [Route("")]
    public class AController : TwinoController
    {
        [HttpGet("c/{?x}")]
        public async Task<IActionResult> C(string x)
        {
            return await StringAsync("hello: " + x);
        }

        [HttpGet("")]
        public async Task<IActionResult> B()
        {
            return await StringAsync("hello");
        }

        [HttpGet("d")]
        public async Task<IActionResult> D()
        {
            return await StringAsync("hello D");
        }

        [HttpGet("e/{x}")]
        public async Task<IActionResult> E(string x)
        {
            return await StringAsync("hello E: " + x);
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
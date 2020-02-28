using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;

namespace Test.Mvc.Controllers
{
    [Route("")]
    [Route("test")]
    [Route("[controller]")]
    public class Test1Controller : TwinoController
    {
        [HttpGet("")]
        public async Task<IActionResult> IndexGet()
        {
            return await StringAsync("get/");
        }

        [HttpPost("")]
        public async Task<IActionResult> IndexPost()
        {
            return await StringAsync("post/");
        }

        [HttpGet("a")]
        public async Task<IActionResult> GetA()
        {
            return await StringAsync("geta/");
        }

        [HttpPost("a")]
        public async Task<IActionResult> PostA()
        {
            return await StringAsync("posta/");
        }

        [HttpGet("b/{?x}")]
        public async Task<IActionResult> GetB(string x)
        {
            return await StringAsync($"getb/{x}");
        }

        [HttpPost("b/{?x}")]
        public async Task<IActionResult> PostB(string x)
        {
            return await StringAsync($"postb/{x}");
        }

        [HttpGet("c/{x}")]
        public async Task<IActionResult> GetC(string x)
        {
            return await StringAsync($"getc/{x}");
        }

        [HttpPost("c/{x}")]
        public async Task<IActionResult> PostC(string x)
        {
            return await StringAsync($"postc/{x}");
        }

        [HttpGet("d/{x}/{?y}")]
        public async Task<IActionResult> GetD(string x, string y)
        {
            return await StringAsync($"getd/{x}/{y}");
        }

        [HttpPost("d/{x}/{?y}")]
        public async Task<IActionResult> PostD(string x, string y)
        {
            return await StringAsync($"postd/{x}/{y}");
        }

        [HttpGet("e/{x}/{y}")]
        public async Task<IActionResult> GetE(string x, string y)
        {
            return await StringAsync($"gete/{x}/{y}");
        }

        [HttpPost("e/{x}/{y}")]
        public async Task<IActionResult> PostE(string x, string y)
        {
            return await StringAsync($"poste/{x}/{y}");
        }


        [HttpGet("[action]")]
        public async Task<IActionResult> GetAA()
        {
            return await StringAsync("geta/");
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> PostAA()
        {
            return await StringAsync("posta/");
        }

        [HttpGet("[action]/{?x}")]
        public async Task<IActionResult> GetBB(string x)
        {
            return await StringAsync($"getb/{x}");
        }

        [HttpPost("[action]/{?x}")]
        public async Task<IActionResult> PostBB(string x)
        {
            return await StringAsync($"postb/{x}");
        }

        [HttpGet("[action]/{x}")]
        public async Task<IActionResult> GetCC(string x)
        {
            return await StringAsync($"getc/{x}");
        }

        [HttpPost("[action]/{x}")]
        public async Task<IActionResult> PostCC(string x)
        {
            return await StringAsync($"postc/{x}");
        }

        [HttpGet("[action]/{x}/{?y}")]
        public async Task<IActionResult> GetDD(string x, string y)
        {
            return await StringAsync($"getd/{x}/{y}");
        }

        [HttpPost("[action]/{x}/{?y}")]
        public async Task<IActionResult> PostDD(string x, string y)
        {
            return await StringAsync($"postd/{x}/{y}");
        }

        [HttpGet("[action]/{x}/{y}")]
        public async Task<IActionResult> GetEE(string x, string y)
        {
            return await StringAsync($"gete/{x}/{y}");
        }

        [HttpPost("[action]/{x}/{y}")]
        public async Task<IActionResult> PostEE(string x, string y)
        {
            return await StringAsync($"poste/{x}/{y}");
        }
    }
}
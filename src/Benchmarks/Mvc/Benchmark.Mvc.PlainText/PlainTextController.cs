using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;

namespace Benchmark.Mvc.PlainText
{
    [Route("plaintext")]
    public class PlainTextController : TwinoController
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return await StringAsync("Hello, World!");
        }
    }
}
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
        public Task<IActionResult> Get()
        {
            return StringAsync("Hello, World!");
        }
    }
}
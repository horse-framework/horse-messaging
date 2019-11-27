using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;

namespace Benchmark.Mvc.Json
{
    [Route("json")]
    public class JsonController : TwinoController
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return await JsonAsync(new {message = "Hello, World!"});
        }
    }
}
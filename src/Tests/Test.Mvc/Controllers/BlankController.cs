using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;

namespace Test.Mvc.Controllers
{
    [Route("")]
    public class BlankController : TwinoController
    {
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            return await StringAsync("/");
        }
    }
}
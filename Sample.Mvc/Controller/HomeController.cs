using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;

namespace Sample.Mvc.Controller
{
    [Route("")]
    public class HomeController : TwinoController
    {
        [HttpGet("")]
        public IActionResult Get()
        {
            return String("Welcome!");
        }
    }
}

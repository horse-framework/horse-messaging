using Twino.Mvc;
using Twino.Mvc.Auth;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;

namespace Sample.Mvc.Controller
{
    [Route("api/[controller]")]
    public class SimpleController : TwinoController
    {
        [HttpGet("go/{?id}")]
        [Authorize(Roles = "Role1,Role2")]
        public IActionResult Go(int? id)
        {
            return String("Go !");
        }

    }
}

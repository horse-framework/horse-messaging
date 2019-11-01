using System.Threading.Tasks;
using Sample.Mvc.Models;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;

namespace Sample.Mvc.Controller
{
    [Route("service")]
    public class ServiceController : TwinoController
    {
        public ServiceController(IFirstService firstService, ISecondService secondService)
        {
            
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            return await StringAsync("Service Get");
        }
    }
}
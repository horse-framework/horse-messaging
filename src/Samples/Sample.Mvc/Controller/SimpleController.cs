using Twino.Mvc.Auth;
using Twino.Mvc.Controllers;
using Twino.Mvc.Controllers.Parameters;
using Twino.Mvc.Filters.Route;
using System;
using System.Collections.Generic;
using System.Text;
using Twino.Mvc;

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

using System;
using System.Net;
using Twino.Mvc.Controllers;
using Twino.Mvc.Controllers.Parameters;
using Twino.Mvc.Filters.Route;
using Twino.Mvc.Results;
using Test.Mvc.Models;
using Twino.Mvc;

namespace Test.Mvc.Controllers
{
    [Route("[controller]")]
    public class HomeController : TwinoController
    {
        [HttpGet("get")]
        public IActionResult Get()
        {
            return Json(new {Ok = true, Message = "Hello world", Code = 200});
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
                return new StatusCodeResult(HttpStatusCode.BadRequest);

            if (model.Username == "root" && model.Password == "password")
                return Json(new {Ok = true, Message = "Welcome", Code = 200});

            return Json(new {Ok = false, Message = "Unauthorized", Code = 401});
        }

        [HttpPut("login-form")]
        public IActionResult LoginForm([FromForm] LoginModel model)
        {
            throw new NotImplementedException();
        }

        [HttpDelete("remove/{id}")]
        public IActionResult Remove(int id)
        {
            throw new NotImplementedException();
        }
    }
}
using Sample.Mvc.Models;
using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Mvc.Auth;
using Twino.Mvc.Auth.Jwt;
using Twino.Mvc.Controllers;
using Twino.Mvc.Controllers.Parameters;
using Twino.Mvc.Filters.Route;

namespace Sample.Mvc.Controller
{
    public class AuthController : TwinoController
    {
        private IJwtProvider _jwtProvider;

        public AuthController()
        {
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            JwtToken token = _jwtProvider.Create("1", "mehmet@example.com", null);
            return Json(token);
        }

        [HttpGet("custom")]
        [Authorize("Custom")]
        public IActionResult Custom()
        {
            return String("custom");
        }

        [HttpGet("it")]
        [Authorize("IT")]
        public IActionResult IT()
        {
            return String("IT");
        }

        [HttpPost("post")]
        public async Task<IActionResult> Post([FromForm] LoginModel model)
        {
            return await JsonAsync(new
            {
                Ok = true,
                Code = 200,
                Message = "Success: " + model.Username
            });
        }
    }
}
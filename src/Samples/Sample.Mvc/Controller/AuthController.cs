using Twino.Mvc;
using Twino.Mvc.Auth;
using Twino.Mvc.Auth.Jwt;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;

namespace Sample.Mvc.Controller
{
    public class AuthController : TwinoController
    {
        private IJwtProvider _jwtProvider;

        public AuthController(IJwtProvider jwtProvider)
        {
            _jwtProvider = jwtProvider;
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

    }
}

using Newtonsoft.Json;

namespace Test.Mvc.Models
{
    public class LoginModel
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }
}
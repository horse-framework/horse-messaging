using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace Sample.Mvc.Models
{
    public class LoginModel
    {
        [JsonProperty("username")]
        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }
}
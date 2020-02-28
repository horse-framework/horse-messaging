using Sample.Mvc.Models;
using System.Net.Http;
using System.Threading.Tasks;
using Twino.Extensions.Http;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;

namespace Sample.Mvc.Controller
{
    [Route("service")]
    public class ServiceController : TwinoController
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ServiceController(IFirstService firstService, ISecondService secondService, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            return await StringAsync("Service Get");
        }

        [HttpGet("getSample")]
        public async Task<IActionResult> GetFromHttpClient()
        {
            HttpClient client = _httpClientFactory.Create();
            var response = await client.GetStringAsync("http://localhost:441/service/get");
            return await JsonAsync(response);
        }
    }
}
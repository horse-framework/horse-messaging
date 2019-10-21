using System.Threading;
using System.Threading.Tasks;
using Twino.Mvc.Controllers;
using Twino.Mvc.Controllers.Parameters;
using Twino.Mvc.Filters.Route;
using Twino.Mvc.Results;

namespace Sample.Mvc.Controller
{
    public class A
    {
        public string B { get; set; }
    }

    [Route("[controller]")]
    public class DemoController : TwinoController
    {
        [HttpGet("geta/{?id}")]
        public async Task<IActionResult> GetA([FromRoute] int? id)
        {
            TaskCompletionSource<IActionResult> completionSource = new TaskCompletionSource<IActionResult>();

            Thread th = new Thread(() =>
            {
                Thread.Sleep(100);
                completionSource.SetResult(String("Hello world: " + id));
            });
            th.Start();

            return await completionSource.Task;
        }

        [HttpGet("getb/{?id}")]
        public async Task<IActionResult> GetB([FromRoute] int? id)
        {
            return await StringAsync("Hello world: " + id);
        }

        [HttpGet("get/{?id}")]
        public IActionResult Get([FromRoute] int? id)
        {
            Thread.Sleep(100);
            return String("Hello world: " + id);
        }

        [HttpGet("get3/{?id}")]
        public IActionResult Get3([FromRoute] int? id)
        {
            return String("Hello world: " + id);
        }
        
        [HttpPost("get2")]
        public IActionResult Get2([FromBody] A a)
        {
            return Json(new
                        {
                            Message = "Hello World 2: "
                        });
        }

        [HttpGet("optional/{?num}")]
        public IActionResult Optional(int? num)
        {
            if (num.HasValue)
                return String($"Value is {num}");

            return String("Has no value");
        }

        [HttpGet("test")]
        public IActionResult Test2()
        {
            return String("Hello world!");
        }

        [HttpGet("redirect")]
        public IActionResult Redirect()
        {
            return StatusCodeResult.Redirect("/other/go");
        }
    }
}
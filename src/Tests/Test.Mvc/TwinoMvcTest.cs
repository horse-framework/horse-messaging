using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Server;
using Test.Mvc.Controllers;
using Twino.Protocols.Http;
using Xunit;

namespace Test.Mvc
{
    public class TwinoMvcTest
    {
        [Fact]
        public void Run()
        {
            TwinoMvc mvc = new TwinoMvc();

            HomeController cont = new HomeController();
            Assert.NotNull(cont);

            mvc.Init();
            Assembly asm = Assembly.GetExecutingAssembly();
            mvc.CreateRoutes(asm);

            TwinoServer server = new TwinoServer(ServerOptions.CreateDefault());
            server.UseMvc(mvc, HttpOptions.CreateDefault());
            server.Start(47442);
            System.Threading.Thread.Sleep(1000);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.GetAsync("http://127.0.0.1:47442/home/get").Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
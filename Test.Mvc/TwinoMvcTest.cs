using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Server;
using Test.Mvc.Controllers;
using Xunit;

namespace Test.Mvc
{
    public class TwinoMvcTest
    {
        [Fact]
        public void RunAsync()
        {
            TwinoMvc mvc = new TwinoMvc();

            HomeController cont = new HomeController();
            Assert.NotNull(cont);

            HostOptions host = mvc.Server.Options.Hosts.FirstOrDefault();
            if (host == null)
                throw new ArgumentNullException();

            host.Port = 441;

            mvc.Init();
            Assembly asm = Assembly.GetExecutingAssembly();
            mvc.CreateRoutes(asm);

            mvc.RunAsync();
            System.Threading.Thread.Sleep(1000);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.GetAsync("http://127.0.0.1:441/home/get").Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void Run()
        {
            TwinoMvc mvc = new TwinoMvc();

            HomeController cont = new HomeController();
            Assert.NotNull(cont);

            HostOptions host = mvc.Server.Options.Hosts.FirstOrDefault();
            if (host == null)
                throw new ArgumentNullException();

            host.Port = 442;
            mvc.Init();
            Assembly asm = Assembly.GetExecutingAssembly();
            mvc.CreateRoutes(asm);

            Task.Factory.StartNew(mvc.Run);
            System.Threading.Thread.Sleep(1000);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.GetAsync("http://127.0.0.1:442/home/get").Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;
using Twino.Protocols.Http;
using Twino.Protocols.TMQ;
using Twino.Protocols.WebSocket;
using Twino.Server;
using Xunit;

namespace Playground
{
    [Route("")]
    [Route("home")]
    public class HomeController : TwinoController
    {
        [HttpGet]
        [HttpGet("a")]
        public IActionResult Index()
        {
            return String("Hello, World!");
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        { 
            TwinoServer server = new TwinoServer();
            
            TwinoMvc mvc = new TwinoMvc();
            mvc.Init();
            mvc.IsDevelopment = true;
            server.UseMvc(mvc);

            server.Start(80);
            server.BlockWhileRunning();
        }
    }
}
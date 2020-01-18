using System;
using Test.Mq.Internal;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.MQ.Queues;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;
using Twino.Protocols.TMQ.Models;
using Twino.Server;

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

            MqServer mq = new MqServer();
            mq.SetDefaultDeliveryHandler(new TestDeliveryHandler(new TestMqServer()));

            mq.Options.AutoChannelCreation = true;
            mq.Options.AutoQueueCreation = true;
            mq.Options.MessageTimeout = TimeSpan.FromSeconds(1000);
            mq.Options.Status = QueueStatus.Push;
            server.UseMqServer(mq);

            server.Start(81);

            TmqClient client = new TmqClient();
            client.Connect("tmq://127.0.0.1:81");
            while (true)
            {
                client.Push("a1", 123, "Hello world", false);
                Console.ReadLine();
                QueueInformation info = client.GetQueueInfo("a1", 123).Result;
                Console.WriteLine(info);
                Console.ReadLine();
            }

            server.BlockWhileRunning();
        }
    }
}
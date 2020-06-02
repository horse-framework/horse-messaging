using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.Core.Protocols;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Data;
using Twino.MQ.Delivery;
using Twino.MQ.Handlers;
using Twino.MQ.Options;
using Twino.MQ.Queues;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Controllers.Parameters;
using Twino.Mvc.Filters.Route;
using Twino.Protocols.Http;
using Twino.Protocols.TMQ;
using Twino.Protocols.WebSocket;
using Twino.Server;

namespace Playground
{
    class Program
    {
        static async Task Main(string[] args)
        {
            MqServerOptions mqOptions = new MqServerOptions();
            mqOptions.AllowMultipleQueues = true;
            mqOptions.AcknowledgeTimeout = TimeSpan.FromSeconds(90);
            mqOptions.MessageTimeout = TimeSpan.FromSeconds(12);

            MqServer Server = new MqServer(mqOptions);
            Server.SetDefaultDeliveryHandler(new JustAllowDeliveryHandler());

            var ch = Server.CreateChannel("ch-1");
            await ch.CreateQueue(101);
            
            ServerOptions serverOptions = ServerOptions.CreateDefault();
            serverOptions.Hosts[0].Port = 88;
            serverOptions.PingInterval = 60;
            serverOptions.RequestTimeout = 60;

            TwinoServer server = new TwinoServer(serverOptions);
            server.UseMqServer(Server);
            server.Start();

            /*
            TwinoServer _server = new TwinoServer();
            _server = new TwinoServer(ServerOptions.CreateDefault());
            _server.UseWebSockets(async (socket) => { await socket.SendAsync("Welcome"); },
                                  async (socket, message) =>
                                  {
                                      Console.WriteLine("# " + message);
                                      await socket.SendAsync(message);
                                  });
            _server.Start(46100);*/
            server.BlockWhileRunning();
        }
    }
}
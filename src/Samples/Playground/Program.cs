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
using Twino.MQ.Data;
using Twino.MQ.Options;
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
            TestMqServer server = new TestMqServer();
            server.Initialize(84);
            server.Start(10000, 10000);
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
            server.Server.Server.BlockWhileRunning();
        }
    }
}
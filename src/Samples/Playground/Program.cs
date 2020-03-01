using System;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Data;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Controllers.Parameters;
using Twino.Mvc.Filters.Route;
using Twino.Protocols.TMQ;
using Twino.Protocols.WebSocket;
using Twino.Server;

namespace Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            TwinoServer _server = new TwinoServer();
            _server = new TwinoServer(ServerOptions.CreateDefault());
            _server.UseWebSockets(async (socket) => { await socket.SendAsync("Welcome"); },
                                  async (socket, message) =>
                                  {
                                      Console.WriteLine("# " + message);
                                      await socket.SendAsync(message);
                                  });
            _server.Start(46100);
            _server.BlockWhileRunning();
        }
    }
}
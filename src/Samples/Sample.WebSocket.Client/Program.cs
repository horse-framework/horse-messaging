using System;
using System.Threading.Tasks;
using Twino.Client.WebSocket;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;
using Twino.Protocols.WebSocket;
using Twino.Server;

namespace Sample.WebSocket.Client
{
    [Route("")]
    public class TController : TwinoController
    {
        [HttpGet("")]
        public IActionResult Get()
        {
            return String(".");
        }
    }

    class Program
    {
        static void StartServer()
        {
            TwinoServer server = new TwinoServer(ServerOptions.CreateDefault());
            server.UseWebSockets(async (socket, data) =>
                                 {
                                     Console.WriteLine("connected");
                                     socket.Disconnected += c => Console.WriteLine("disconnected");
                                     await Task.CompletedTask;
                                 },
                                 async (socket, message) =>
                                 {
                                     Console.Write(message);
                                     await socket.SendAsync(message);
                                 });

            server.Options.PingInterval = 30;
            server.Start();
        }

        static void ConnectWithTwino()
        {
            TwinoWebSocket cx = new TwinoWebSocket();
            cx.MessageReceived += (c, m) => Console.WriteLine("# " + m);
            cx.Connected += c => Console.WriteLine("Connected");
            cx.Disconnected += c => Console.WriteLine("Disconnected");
            cx.Connect("ws://127.0.0.1:83");

            while (true)
            {
                string s = Console.ReadLine();
                cx.Send(s);
            }
        }

        static void Main(string[] args)
        {
            StartServer();
            ConnectWithTwino();
        }
    }
}
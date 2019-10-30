using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;
using Twino.Server;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Twino.Client;
using Twino.Server.WebSockets;

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
            TwinoServer server = TwinoServer.CreateWebSocket(async (s, req, tcp) =>
                                                         {
                                                             ServerSocket sock = new ServerSocket(s, req, tcp);
                                                             sock.Connected += c => Console.WriteLine("connected");
                                                             sock.Disconnected += c => Console.WriteLine("disconnected");
                                                             sock.MessageReceived += (c, m) =>
                                                                                     {
                                                                                         Console.Write(m);
                                                                                         c.Send(m);
                                                                                     };
                                                             
                                                             return await Task.FromResult(sock);
                                                         });
            
            server.Options.PingInterval = 30000;
            server.Start();
        }

        static void ConnectWithTwino()
        {
            TwinoClient cx = new TwinoClient();
            cx.MessageReceived += (c, m) => Console.WriteLine("# " + m);
            cx.Connected += c => Console.WriteLine("Connected"); 
            cx.Disconnected += c => Console.WriteLine("Disconnected"); 
            cx.Connect("ws://127.0.0.1:84");
            while (true)
            {
                string s = Console.ReadLine();
                cx.Send(s);
            }
        }
        
        static void Main(string[] args)
        {
            //StartServer();

            //ConnectWithTcpClient();

           // Console.ReadLine();

            ConnectWithTwino();
            
        }

    }
}

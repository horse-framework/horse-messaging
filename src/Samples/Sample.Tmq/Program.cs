using System;
using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.Core;
using Twino.Protocols.TMQ;
using Twino.Protocols.WebSocket;
using Twino.Server;

namespace Sample.Tmq
{
    class Program
    {
        static void Main(string[] args)
        {
            TwinoServer server = new TwinoServer(ServerOptions.CreateDefault());
            server.UseTmq(async (socket, msg) =>
            {
                Console.WriteLine(msg);
                await Task.CompletedTask;
            });
            server.Start(82);

            TmqClient client = new TmqClient();
            client.Data.Properties.Add("Host", "localhost");
            client.ClientId = "123";

            client.Connect("tmq://localhost:82/sample");

            Console.ReadLine();
        }
    }
}
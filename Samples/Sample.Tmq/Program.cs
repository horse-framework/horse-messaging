using System;
using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.Protocols.TMQ;
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
            client.Connect("tmq://localhost:82");
            
            Console.WriteLine("Hello World!");
        }
    }
}
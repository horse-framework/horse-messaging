using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace ClusteringSample.Producer
{
    public class Foo
    {
        public int No { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            HorseClient client = new HorseClient();
            
            client.AddHost("horse://localhost:26101");
            client.AddHost("horse://localhost:26102");
            client.AddHost("horse://localhost:26103");
            
            await client.ConnectAsync();

            int no = 1;
            while (true)
            {
                Console.ReadLine();

                if (!client.IsConnected)
                {
                    Console.Write("Client is not connected ");
                    continue;
                }

                HorseResult result = await client.Queue.PushJson(new Foo {No = no}, true);
                Console.Write($"Message #{no} Push Result {result.Code} ");
                
                if (result.Code == HorseResultCode.Ok)
                    no++;
            }
        }
    }
}
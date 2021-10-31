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
            
            client.AddHost("horse://localhost:26222");
            client.AddHost("horse://localhost:26102");
            client.AddHost("horse://localhost:26103");
            
            await client.ConnectAsync();

            int no = 1;
            bool hasConnection = true;
            while (true)
            {
               await Task.Delay(200);

                if (!client.IsConnected)
                {
                    hasConnection = false;
                    Console.Write(".");
                    continue;
                }

                if (!hasConnection)
                {
                    Console.WriteLine();
                    hasConnection = true;
                }

                HorseResult result = await client.Queue.PushJson(new Foo {No = no}, true);
                Console.WriteLine($"Message #{no} Push Result {result.Code} ");
                
                if (result.Code == HorseResultCode.Ok)
                    no++;
            }
        }
    }
}
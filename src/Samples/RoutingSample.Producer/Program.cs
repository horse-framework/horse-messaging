using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using RoutingSample.Models;

namespace RoutingSample.Producer
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            HorseClient client = new HorseClient();
            client.MessageSerializer = new NewtonsoftContentSerializer();
            await client.ConnectAsync("horse://localhost:15500");

            while (true)
            {
                HorseResult result = await client.Router.PublishJson(new SampleMessage(), true);
                Console.WriteLine($"Push: {result.Code}");
                await Task.Delay(5000);
            }
        }
    }
}
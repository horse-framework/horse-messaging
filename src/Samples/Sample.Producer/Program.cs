using System;
using System.Threading.Tasks;
using Horse.Messaging.Server.Client;
using Horse.Messaging.Server.Client.Bus;
using Horse.Messaging.Server.Protocol;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;

namespace Sample.Producer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            HmqStickyConnector connector = new HmqStickyConnector(TimeSpan.FromSeconds(2));
            connector.AddHost("horse://localhost:9999");
            connector.ContentSerializer = new NewtonsoftContentSerializer();
            connector.Run();

            IHorseQueueBus queueBus = connector.Bus.Queue;

            ModelA a = new ModelA();
            a.Foo = "foo";
            a.No = 123;

            while (true)
            {
                HorseResult result = await queueBus.PushJson(a);
                Console.WriteLine($"Push: {result.Code}");
                await Task.Delay(5000);
            }
        }
    }
}
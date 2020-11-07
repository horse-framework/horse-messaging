using System;
using System.Threading.Tasks;
using Twino.MQ.Client;
using Twino.MQ.Client.Bus;
using Twino.MQ.Client.Connectors;
using Twino.Protocols.TMQ;

namespace Sample.Producer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            TmqStickyConnector connector = new TmqStickyConnector(TimeSpan.FromSeconds(2));
            connector.AddHost("tmq://localhost:26222");
            connector.ContentSerializer = new NewtonsoftContentSerializer();
            connector.Run();
            
            ITwinoQueueBus queueBus = connector.Bus.Queue;

            ModelA a = new ModelA();
            a.Foo = "foo";
            a.No = 123;

            while (true)
            {
                TwinoResult result = await queueBus.PushJson(a);
                Console.WriteLine($"Push: {result.Code}");
                await Task.Delay(5000);
            }
        }
    }
}
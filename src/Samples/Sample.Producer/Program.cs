using System;
using System.Threading.Tasks;
using Horse.Mq.Client;
using Horse.Mq.Client.Bus;
using Horse.Mq.Client.Connectors;
using Horse.Protocols.Hmq;

namespace Sample.Producer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            HmqStickyConnector connector = new HmqStickyConnector(TimeSpan.FromSeconds(2));
            connector.AddHost("hmq://localhost:26222");
            connector.ContentSerializer = new NewtonsoftContentSerializer();
            connector.Run();
            
            IHorseQueueBus queueBus = connector.Bus.Queue;

            ModelA a = new ModelA();
            a.Foo = "foo";
            a.No = 123;

            while (true)
            {
                HorseResult result = await queueBus.PushJson("Username1", a);
                Console.WriteLine($"Push: {result.Code}");
                await Task.Delay(5000);
            }
        }
    }
}
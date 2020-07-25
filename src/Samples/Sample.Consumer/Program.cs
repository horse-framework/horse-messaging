using System;
using System.Threading.Tasks;
using Sample.Consumer.Consumers;
using Twino.Client.TMQ.Connectors;
using Twino.Protocols.TMQ;

namespace Sample.Consumer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            TmqStickyConnector connector = new TmqStickyConnector(TimeSpan.FromSeconds(1));

            connector.AutoJoinConsumerChannels = true;
            connector.InitJsonReader();

            connector.AddProperty(TmqHeaders.CLIENT_NAME, "consumer");

            connector.Consumer.RegisterAssemblyConsumers(typeof(Program));
            //connector.Consumer.RegisterConsumer<DirectConsumerC>();
            //connector.Consumer.RegisterConsumer<QueueConsumerA>();

            connector.AddHost("tmq://127.0.0.1:22200");
            connector.Run();

            while (true)
                await Task.Delay(1000);
        }
    }
}
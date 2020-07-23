using System;
using System.Threading.Tasks;
using Twino.Client.TMQ.Connectors;

namespace Sample.Consumer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            TmqStickyConnector connector = new TmqAbsoluteConnector(TimeSpan.FromSeconds(1));
            
            connector.AutoJoinConsumerChannels = true;
            connector.InitJsonReader();
            connector.Consumer.RegisterAssemblyConsumers(typeof(Program));

            connector.AddHost("tmq://127.0.0.1:22200");
            connector.Run();

            while (true)
                await Task.Delay(1000);
        }
    }
}
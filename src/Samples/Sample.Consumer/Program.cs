using System;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Connectors;
using Twino.Ioc;

namespace Sample.Consumer
{
    class Program
    {
        public static IServiceContainer Services { get; } = new ServiceContainer();

        static void Main(string[] args)
        {
            TmqStickyConnector connector = new TmqStickyConnector(TimeSpan.FromSeconds(2));
            connector.AddHost("tmq://localhost:26222");
            connector.ContentSerializer = new NewtonsoftContentSerializer();
            connector.Observer.RegisterConsumer<ModelAConsumer>();
            connector.Connected += (c) => { _ = connector.GetClient().Queues.Subscribe("model-a", false); };
            connector.Run();

            while (true)
                Console.ReadLine();
        }
    }
}
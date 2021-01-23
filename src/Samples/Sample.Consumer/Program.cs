using System;
using Horse.Mq.Bus;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;

namespace Sample.Consumer
{
    class Program
    {
        static void Main(string[] args)
        {
            HmqStickyConnector connector = new HmqStickyConnector(TimeSpan.FromSeconds(2));
            connector.AddHost("hmq://localhost:26222");
            connector.ContentSerializer = new NewtonsoftContentSerializer();
            connector.Observer.RegisterConsumer<ModelAConsumer>();
            connector.Run();

            HorseConnectorBuilder builder = new HorseConnectorBuilder();


            builder.AddHost("host")
                   .AddTransientConsumers(typeof(Program))
                   .ConfigureModels(cfg => cfg.UseQueueName(type => type.Name)
                                              .UseConsumerAck()
                                              .AddMessageHeader("Sender-Client-Name", "MyName")
                                              .SetPutBackDelay(TimeSpan.FromSeconds(10)));


            while (true)
                Console.ReadLine();
        }
    }
}
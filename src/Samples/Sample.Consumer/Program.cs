using System;
using Microsoft.Extensions.DependencyInjection;
using Twino.MQ.Bus;
using Twino.MQ.Client;
using Twino.MQ.Client.Connectors;

namespace Sample.Consumer
{
    class Program
    {
        static void Main(string[] args)
        {
            IServiceCollection services = new ServiceCollection();
            TmqStickyConnector connector = new TmqStickyConnector(TimeSpan.FromSeconds(2));
            connector.AddHost("tmq://localhost:26222");
            connector.ContentSerializer = new NewtonsoftContentSerializer();
            connector.Observer.RegisterConsumer<ModelAConsumer>();
            connector.Run();

            TwinoConnectorBuilder builder = new TwinoConnectorBuilder();


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
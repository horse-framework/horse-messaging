using System;
using Horse.Messaging.Server.Bus;

namespace Sample.Consumer
{
    class Program
    {
        static void Main(string[] args)
        {
            HorseConnectorBuilder builder = new HorseConnectorBuilder();

            builder.AddHost("horse://localhost:26222")
                   .AddTransientConsumers(typeof(Program))
                   .ConfigureModels(cfg => cfg.UseQueueName(type => "Username1")
                                              .UseConsumerAck()
                                              .AddMessageHeader("Sender-Client-Name", "MyName")
                                              .SetPutBackDelay(TimeSpan.FromSeconds(10)))
                   .Build()
                   .Run();


            while (true)
                Console.ReadLine();
        }
    }
}
# Horse MQ

[![NuGet](https://img.shields.io/nuget/v/Horse.MQ?label=server%20nuget)](https://www.nuget.org/packages/Horse.MQ)
[![NuGet](https://img.shields.io/nuget/v/Horse.MQ.Client?label=client%20nuget)](https://www.nuget.org/packages/Horse.MQ.Client)
[![NuGet](https://img.shields.io/nuget/v/Horse.MQ.Bus?label=bus%20nuget)](https://www.nuget.org/packages/Horse.MQ.Bus)

* Horse MQ is a .NET Core messaging queue and communication framework.
* Horse MQ is not only queue messaging server. In provides direct messages, requests, responses and getting server and client informations.
* Horse MQ has many queue messaging structure: Broadcast, Push, Pull, Cache.
* Clients can subscribe events and gets information when another client is connected or does something.

### Very Quick Messaging Queue Server Example

    class Program
    {
        static void Main(string[] args)
        {
            HorseServer server = new HorseServer();
            server.UseHorseMq(cfg => cfg.UseJustAllowDeliveryHandler());
            server.Run(22200);
        }
    }
    
### Very Quick Server Example with Persistent Queues

    class Program
    {
        static async Task Main(string[] args)
        {
            HorseServer server = new HorseServer();
            HorseMq mq = server.UseHorseMq(cfg => cfg
                                                  .AddPersistentQueues(q => q.KeepLastBackup())
                                                  .UsePersistentDeliveryHandler(DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.AfterReceived));

            await mq.LoadPersistentQueues();
            server.Run(22200);
        }
    }

### Consumer without Horse.Extensions.Bus

Implementation

        static async Task Main(string[] args)
        {
            HmqStickyConnector connector = new HmqStickyConnector(TimeSpan.FromSeconds(1));
            connector.AutoSubscribe = true;
            connector.ContentSerializer = new NewtonsoftContentSerializer();
            connector.Observer.RegisterAssemblyConsumers(typeof(Program));
            connector.AddHost("tmq://127.0.0.1:22200");
            connector.Run();
            while (true)
                await Task.Delay(1000);
        }

Model

    [QueueName("model-a")]
    public class ModelA
    {
        public string Foo { get; set; }
    }

Consumer

    [AutoAck]
    [AutoNack]
    public class QueueConsumerA : IQueueConsumer<ModelA>
    {
        public Task Consume(HorseMessage message, ModelA model, HorseClient client)
        {
            Console.WriteLine("Model A Consumed");
            return Task.CompletedTask;
        }
    }


### Consumer with Horse.Extensions.Bus

Model and Consumer same with example above. The Implementation change is here:

    services.UseHorseBus(cfg => cfg.AddHost("tmq://127.0.0.1:22200")
                                   .AddTransientConsumers(typeof(Program)));


If you use a service provider, you can inject other services to consumer objects.


### Sending Messages as Producer

Horse accepts producers and consumers as client. Each client can be producer and consumer at same time. With ConsumerFactory implementation, you can inject IHorseBus interface for being producer at same time. If you want to create only producer, you can skip Add..Consumers methods.

     IHorseQueueBus queueBus;   //injected
     IHorseRouteBus routeBus;   //injected
     IHorseDirectBus directBus; //injected

     //push to a queue
     await queueBus.PushJson(new ModelA());

     //publish to a router
     await routeBus.PublishJson(new ModelA());

     //to a direct target, ModelA requires DirectTarget attribute
     //and ContentType attribute will be useful to recognize message type by receiver
     directBus.SendJson(new ModelA());



## Thanks

Thanks to JetBrains for open source license to use on this project.

[![jetbrains](https://user-images.githubusercontent.com/21208762/90192662-10043700-ddcc-11ea-9533-c43b99801d56.png)](https://www.jetbrains.com/?from=twino-framework)

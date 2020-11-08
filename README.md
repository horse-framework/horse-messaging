# Twino MQ

[![NuGet](https://img.shields.io/nuget/v/Twino.MQ?label=server%20nuget)](https://www.nuget.org/packages/Twino.MQ)
[![NuGet](https://img.shields.io/nuget/v/Twino.MQ.Client?label=client%20nuget)](https://www.nuget.org/packages/Twino.MQ.Client)
[![NuGet](https://img.shields.io/nuget/v/Twino.MQ.Bus?label=bus%20nuget)](https://www.nuget.org/packages/Twino.MQ.Bus)

* Twino MQ is a .NET Core messaging queue and communication framework.
* Twino MQ is not only queue messaging server. In provides direct messages, requests, responses and getting server and client informations.
* Twino MQ has many queue messaging structure: Broadcast, Push, Pull, Cache.
* Clients can subscribe events and gets information when another client is connected or does something.

If you want to go further about Twino MQ infrastructure you can read specification and documentation from [here](https://github.com/twino-framework/twino-mq/blob/master/docs/twino-mq.pdf)<br><br>

### Very Quick Messaging Queue Server Example

    class Program
    {
        static Task Main(string[] args)
        {
            TwinoServer server = new TwinoServer();
            server.UseTwinoMQ(cfg => cfg.UseJustAllowDeliveryHandler());
            server.Start(22200);
            await server.BlockWhileRunningAsync();
        }
    }
    
### Very Quick Server Example with Persistent Queues

    class Program
    {
        static Task Main(string[] args)
        {
            TwinoServer server = new TwinoServer();
            TwinoMQ mq = server.UseTwinoMQ(cfg => cfg
                                                  .AddPersistentQueues(q => q.KeepLastBackup())
                                                  .UsePersistentDeliveryHandler(DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.AfterReceived));

            await mq.LoadPersistentQueues();
            server.Start(22200);
            await server.BlockWhileRunningAsync();
        }
    }

### Consumer without ConsumerFactory Implementation

Implementation

        static async Task Main(string[] args)
        {
            TmqStickyConnector connector = new TmqStickyConnector(TimeSpan.FromSeconds(1));
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
        public Task Consume(TwinoMessage message, ModelA model, TmqClient client)
        {
            Console.WriteLine("Model A Consumed");
            return Task.CompletedTask;
        }
    }


### Consumer with ConsumerFactory Implementation

Model and Consumer same with example above. The Implementation change is here:

    services.UseTwinoBus(cfg => cfg.AddHost("tmq://127.0.0.1:22200")
                                   .AddTransientConsumers(typeof(Program)));


If you use a service provider, you can inject other services to consumer objects.


### Sending Messages as Producer

Twino accepts producers and consumers as client. Each client can be producer and consumer at same time. With ConsumerFactory implementation, you can inject ITwinoBus interface for being producer at same time. If you want to create only producer, you can skip Add..Consumers methods.

     ITwinoQueueBus queueBus;   //injected
     ITwinoRouteBus routeBus;   //injected
     ITwinoDirectBus directBus; //injected

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
